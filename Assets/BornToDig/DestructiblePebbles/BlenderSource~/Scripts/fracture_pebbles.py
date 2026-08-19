import bpy
import bmesh
import os
import random
import shutil
import sys
from mathutils import Matrix, Vector


BASE_OFFSETS = (
    (-0.34, -0.16, -0.10),
    (0.32, -0.14, 0.12),
    (-0.10, 0.32, 0.13),
    (0.13, 0.05, 0.34),
    (0.03, 0.08, -0.34),
)


def parse_arguments():
    try:
        separator = sys.argv.index("--")
    except ValueError as error:
        raise RuntimeError("Expected arguments after --: <A|B|C> <output root>") from error

    arguments = sys.argv[separator + 1:]
    if len(arguments) != 2:
        raise RuntimeError("Expected arguments after --: <A|B|C> <output root>")

    rock_id = arguments[0].upper()
    if rock_id not in {"A", "B", "C"}:
        raise RuntimeError(f"Unsupported rock id: {rock_id}")
    return rock_id, os.path.abspath(arguments[1])


def find_source_mesh():
    candidates = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not candidates:
        raise RuntimeError("The source Blend contains no mesh object.")
    return max(candidates, key=lambda obj: len(obj.data.vertices))


def create_applied_mesh(source):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    mesh.transform(source.matrix_world)
    mesh.update()
    return mesh


def mesh_bounds(mesh):
    coordinates = [vertex.co for vertex in mesh.vertices]
    minimum = Vector((
        min(value.x for value in coordinates),
        min(value.y for value in coordinates),
        min(value.z for value in coordinates),
    ))
    maximum = Vector((
        max(value.x for value in coordinates),
        max(value.y for value in coordinates),
        max(value.z for value in coordinates),
    ))
    return minimum, maximum


def object_world_bounds(obj):
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    minimum = Vector((
        min(value.x for value in points),
        min(value.y for value in points),
        min(value.z for value in points),
    ))
    maximum = Vector((
        max(value.x for value in points),
        max(value.y for value in points),
        max(value.z for value in points),
    ))
    return minimum, maximum


def calculate_world_volume(obj):
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bm.transform(obj.matrix_world)
        return abs(bm.calc_volume(signed=True))
    finally:
        bm.free()


def count_non_manifold_edges(obj):
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        return sum(1 for edge in bm.edges if not edge.is_manifold)
    finally:
        bm.free()


def clear_scene_objects():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)


def create_seed_points(rock_id, minimum, maximum):
    center = (minimum + maximum) * 0.5
    extents = (maximum - minimum) * 0.5
    generator = random.Random(8100 + ord(rock_id))
    seeds = []

    for base in BASE_OFFSETS:
        jitter = Vector((
            generator.uniform(-0.035, 0.035),
            generator.uniform(-0.035, 0.035),
            generator.uniform(-0.035, 0.035),
        ))
        normalized = Vector(base) + jitter
        seeds.append(center + Vector((
            normalized.x * extents.x,
            normalized.y * extents.y,
            normalized.z * extents.z,
        )))

    return seeds


def fill_open_boundaries(bm):
    boundary_edges = [edge for edge in bm.edges if edge.is_boundary]
    if boundary_edges:
        bmesh.ops.holes_fill(bm, edges=boundary_edges, sides=0)


def create_fragment_mesh(intact_mesh, seeds, fragment_index, mesh_name):
    bm = bmesh.new()
    try:
        bm.from_mesh(intact_mesh)
        own_seed = seeds[fragment_index]

        for other_index, other_seed in enumerate(seeds):
            if other_index == fragment_index:
                continue

            plane_normal = other_seed - own_seed
            if plane_normal.length_squared < 1e-10:
                raise RuntimeError("Fracture seed points overlap.")

            plane_normal.normalize()
            plane_point = (own_seed + other_seed) * 0.5
            geometry = list(bm.verts) + list(bm.edges) + list(bm.faces)
            bmesh.ops.bisect_plane(
                bm,
                geom=geometry,
                dist=1e-7,
                plane_co=plane_point,
                plane_no=plane_normal,
                use_snap_center=False,
                clear_outer=True,
                clear_inner=False,
            )
            fill_open_boundaries(bm)

        if not bm.faces:
            raise RuntimeError(f"Fragment {fragment_index + 1} is empty.")

        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bmesh.ops.triangulate(bm, faces=list(bm.faces))

        origin = sum((vertex.co for vertex in bm.verts), Vector()) / len(bm.verts)
        bm.transform(Matrix.Translation(-origin))

        fragment_mesh = bpy.data.meshes.new(mesh_name)
        bm.to_mesh(fragment_mesh)
        fragment_mesh.update()
        for material in intact_mesh.materials:
            fragment_mesh.materials.append(material)
        return fragment_mesh, origin
    finally:
        bm.free()


def export_selected(filepath):
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
        bake_anim=False,
    )


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def save_texture_copies(rock_id, texture_directory):
    os.makedirs(texture_directory, exist_ok=True)
    names = {
        "base_color": "Pebble_BaseColor.png",
        "normal": "Pebble_Normal.png",
        "metallic_roughness": "Pebble_MetallicRoughness.png",
    }

    for image in bpy.data.images:
        normalized_name = image.name.lower()
        role = next((key for key in names if key in normalized_name), None)
        if role is None or image.size[0] <= 0 or image.size[1] <= 0:
            continue

        destination = os.path.join(texture_directory, names[role])
        image.filepath_raw = destination
        image.file_format = "PNG"
        image.save()
        print(f"TEXTURE_SAVED role={role} path={destination}")


def validate(intact, fragments):
    intact_volume = calculate_world_volume(intact)
    fragment_volumes = [calculate_world_volume(fragment) for fragment in fragments]
    fragment_total = sum(fragment_volumes)
    ratio = fragment_total / intact_volume if intact_volume > 0 else 0.0

    intact_minimum, intact_maximum = object_world_bounds(intact)
    fragment_bounds = [object_world_bounds(fragment) for fragment in fragments]
    reconstructed_minimum = Vector((
        min(bounds[0].x for bounds in fragment_bounds),
        min(bounds[0].y for bounds in fragment_bounds),
        min(bounds[0].z for bounds in fragment_bounds),
    ))
    reconstructed_maximum = Vector((
        max(bounds[1].x for bounds in fragment_bounds),
        max(bounds[1].y for bounds in fragment_bounds),
        max(bounds[1].z for bounds in fragment_bounds),
    ))
    bound_error = max(
        (intact_minimum - reconstructed_minimum).length,
        (intact_maximum - reconstructed_maximum).length,
    )
    non_manifold = [count_non_manifold_edges(fragment) for fragment in fragments]

    print(
        f"FRACTURE_VALIDATION intact_volume={intact_volume:.9f} "
        f"fragment_volumes={[round(value, 9) for value in fragment_volumes]} "
        f"volume_ratio={ratio:.9f} bound_error={bound_error:.9f} "
        f"non_manifold_edges={non_manifold}"
    )
    print(
        "FRACTURE_BOUNDS "
        f"intact_min={tuple(round(value, 9) for value in intact_minimum)} "
        f"intact_max={tuple(round(value, 9) for value in intact_maximum)} "
        f"fragments_min={tuple(round(value, 9) for value in reconstructed_minimum)} "
        f"fragments_max={tuple(round(value, 9) for value in reconstructed_maximum)}"
    )

    if len(fragments) != 5:
        raise RuntimeError(f"Expected 5 fragments, got {len(fragments)}")
    if any(value <= intact_volume * 0.03 for value in fragment_volumes):
        raise RuntimeError("At least one fragment is too small for stable physics.")
    if not 0.995 <= ratio <= 1.005:
        raise RuntimeError(f"Fragment volume ratio is outside tolerance: {ratio}")
    if bound_error > 1e-4:
        raise RuntimeError(f"Reconstructed bounds differ from intact bounds: {bound_error}")
    if any(non_manifold):
        raise RuntimeError(f"Fragments are not closed manifold meshes: {non_manifold}")


def main():
    rock_id, output_root = parse_arguments()
    source_path = bpy.data.filepath
    if not source_path or not os.path.isfile(source_path):
        raise RuntimeError("A saved source Blend must be opened before running this script.")

    models_directory = os.path.join(output_root, "Models")
    textures_directory = os.path.join(output_root, "Textures")
    blender_directory = os.path.join(output_root, "BlenderSource~")
    originals_directory = os.path.join(blender_directory, "Originals")
    os.makedirs(models_directory, exist_ok=True)
    os.makedirs(originals_directory, exist_ok=True)

    original_copy = os.path.join(originals_directory, os.path.basename(source_path))
    shutil.copy2(source_path, original_copy)
    print(f"ORIGINAL_COPIED source={source_path} destination={original_copy}")

    source = find_source_mesh()
    applied_mesh = create_applied_mesh(source)
    clear_scene_objects()

    intact_collection = bpy.data.collections.new(f"Rock_{rock_id}_Intact")
    fractured_collection = bpy.data.collections.new(f"Rock_{rock_id}_Fractured")
    bpy.context.scene.collection.children.link(intact_collection)
    bpy.context.scene.collection.children.link(fractured_collection)

    intact = bpy.data.objects.new(f"Rock_{rock_id}_Intact", applied_mesh)
    intact_collection.objects.link(intact)
    minimum, maximum = mesh_bounds(applied_mesh)
    intact_origin = (minimum + maximum) * 0.5
    applied_mesh.transform(Matrix.Translation(-intact_origin))
    intact.location = intact_origin
    intact.rotation_euler = (0.0, 0.0, 0.0)
    intact.scale = (1.0, 1.0, 1.0)

    local_minimum, local_maximum = mesh_bounds(applied_mesh)
    seeds = create_seed_points(rock_id, local_minimum, local_maximum)
    fragments = []
    for index in range(5):
        object_name = f"Rock_{rock_id}_Fragment_{index + 1:02d}"
        fragment_mesh, fragment_origin = create_fragment_mesh(
            applied_mesh,
            seeds,
            index,
            f"{object_name}_Mesh",
        )
        fragment = bpy.data.objects.new(object_name, fragment_mesh)
        fragment.location = intact_origin + fragment_origin
        fragment.rotation_euler = (0.0, 0.0, 0.0)
        fragment.scale = (1.0, 1.0, 1.0)
        fractured_collection.objects.link(fragment)
        fragments.append(fragment)

    bpy.context.view_layer.update()
    validate(intact, fragments)
    save_texture_copies(rock_id, textures_directory)

    select_only([intact])
    export_selected(os.path.join(models_directory, f"Rock_{rock_id}_Intact.fbx"))
    select_only(fragments)
    export_selected(os.path.join(models_directory, f"Rock_{rock_id}_Fractured.fbx"))

    intact.hide_render = False
    intact.hide_viewport = False
    fractured_collection.hide_render = True
    blend_output = os.path.join(blender_directory, f"Rock_{rock_id}_Destruction.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_output, check_existing=False)

    print(
        f"PEBBLE_FRACTURE_PASS rock={rock_id} intact={intact.name} "
        f"fragments={[fragment.name for fragment in fragments]} blend={blend_output}"
    )


if __name__ == "__main__":
    main()
