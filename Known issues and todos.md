
Please note it's not an urgency list. Just because something is marked as "high" doesn't mean it will
be implemented first. It means it's "must have" for 1.0, but alpha/beta issues can be implemented in any order.

Medium:
	-	[Improvement] Enemies should do some kind of effect when they bounce after being hit
	-	[Improvement] Remove all .blend files after converting them to .glb. 
		Blend files are too heavy. This is necessary to put the code into Git repo, remove
		requirement for Blender and make import faster. The biggest ones have
		already been converted, but Blender dependency is still there.

Low:
	-	[Improvement] Not all places in main menu have working "back" button. They require scrolling. 
	-	[Improvement] Pipes only support 1 player at the time.
	-	[Improvement] Time records don't take characters into consideration. Save file format
		already supports it, but all records are saved and loaded only as "Sonic".
	-	[Feature] More HSON integration. For example - triggers.
	-	[Improvement] Water running effects
