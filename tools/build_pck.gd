extends SceneTree

func _init() -> void:
	var args: PackedStringArray = OS.get_cmdline_user_args()
	if args.size() < 2:
		push_error("Usage: godot --headless --script build_pck.gd -- <pack_root> <output_pck>")
		quit(1)
		return

	var pack_root := args[0]
	var output_pck := args[1]

	var packer := PCKPacker.new()
	var err := packer.pck_start(output_pck)
	if err != OK:
		push_error("pck_start failed: %s" % err)
		quit(1)
		return

	err = _add_dir_recursive(packer, pack_root, "")
	if err != OK:
		push_error("add_dir failed: %s" % err)
		quit(1)
		return

	err = packer.flush()
	if err != OK:
		push_error("flush failed: %s" % err)
		quit(1)
		return

	print("PCK built: %s" % output_pck)
	quit(0)

func _add_dir_recursive(packer: PCKPacker, root: String, rel: String) -> int:
	var abs_dir := root.path_join(rel)
	var dir := DirAccess.open(abs_dir)
	if dir == null:
		return ERR_CANT_OPEN

	for file_name in dir.get_files():
		var abs_file := abs_dir.path_join(file_name)
		var rel_file := (rel.path_join(file_name) if rel != "" else file_name).replace("\\", "/")
		var err := packer.add_file(rel_file, abs_file)
		if err != OK:
			push_error("Failed add_file %s (%s): %s" % [abs_file, rel_file, err])
			return err

	for dir_name in dir.get_directories():
		var next_rel := (rel.path_join(dir_name) if rel != "" else dir_name)
		var err := _add_dir_recursive(packer, root, next_rel)
		if err != OK:
			return err

	return OK
