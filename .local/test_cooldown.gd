extends Node

func _ready():
    var wf = load("res://src/weapons/WeaponFactory.cs")
    # This won't work in GDScript, but we can check the data structure
    print("Test script loaded")
