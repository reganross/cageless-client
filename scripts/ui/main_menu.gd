extends Control


func _on_connect_pressed() -> void:
	# Hook for server connection flow (address, handshake, scene change).
	print("Connect to server…")


func _on_exit_pressed() -> void:
	get_tree().quit()
