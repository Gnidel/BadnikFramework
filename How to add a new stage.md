# Step 1: Make terrain
- Open a 3D modeling software (like Blender) and make terrain model with it.
- Save it somewhere in the project directory
- Double click on the model in Godot
- For each collidable mesh:
-- Select a mesh
-- Check Generate -> Physics to true
-- Select Physics -> Shape Type to Trimesh
-Reimport

# Step 2: Create a stage
- Search for EmptyStage.tscn
- Duplicate it and save in your preferred directory
- Open it. It will contain every object you need, which you can move into preferred locations. Players will spawn on position of PlayersManager.
- Drag & Drop the terrain

# Step 3: Configure StageData and GoalRing
- Set stage and act name
- Set Stage Music (preferred format is .ogg because it supports looping)
- Set Stage Unique Identifier to something unique. You will need it later.

# Step 4: Configure GoalRing
- Set your next stage to the PATH of the next stage, menu, cutscene or whatever will be next. You can get it by right clicking .tscn of the next scene and choosing "Copy Path".
- If you want to unlock the next level in the menu, add it in StagesToUnlock. Use Stage Unique Identifier for it.

# Step 5: Add your stage to the menu
- Open main_menu.tscn
- Create a new Node as a child of StagesData with a script MainMenuStagesData
- Set StageUniqueIdentifier, Stage Name and Act Name to be the same as in Step 3.
- Click "Unlocked By Default" if you want the stage to be unlocked from the start

# Step 6: You're done!
You're done with the basic setup. Now you can focus on the stage itself and unleash your creativity.
Useful objects are marked with [Interactable], [Enemy] and [Item] tags in their names.
You can also check TestStage.tscn for examples how to do certain things and tips.
