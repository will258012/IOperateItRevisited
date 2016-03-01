# IOperateIt

A driving vehicle simulator, in the vein of the U-drive-it mode of Simcity 4 and Streets of Simcity. 
The mod lets you operate any vehicle currently driving around the city, or spawn any vehicle on a road segment with 3 view angles:

* Far view
* Follow up
* First person

-------------------

# Controls
To enter driving mode, (optionally) select a vehicle from the "Vehicle Selector" panel, click on the "Spawn vehicle" button, and click on a road segment. Alternatively, clicking on the "Drive This Vehicle" button in the vehicle info panel will spawn a copy of the vehicle in the same spot.
 
Once in driving mode, controls are:

* Arrow keys: Control the vehicle
* F2: Exit driving mode
* F3: Switch viewing mode

-------------------

# Operation

In this version(just testing/pre-pre-alpha), 
the vehicle renders the selected vehicle's mesh from its VehicleInfo object, and adds a single rigidbody to enable physics. 

Forward/backward acceleration are implemented by applying forward/backward force, and rotation is achieved by applying a breaking force and adding torque.

Once the velocity exceeds the maximum limit, a breaking force is applied to slow the vehicle down.

-------------------

# Upcoming Features

* Configurable control forces
* Configurable control keys
* More documentation

-------------------

# Limitations

## Physics
With the current version, there is no physics interation with in-game objects( roads, buildings ), which means things like the rotation of the vehicle and collision detection aren't available yet. While physics can be enabled for buidldings easily, road segments will likely detouring/overriding the road rendering functionality to include enabling physics.

-------------------

# Acknowledgements

* [FPS Camera for game panel extender and camera positioning ](https://github.com/AlexanderDzhoganov/Skylines-FPSCamera)
* [Skylines-Multiplayer for the character control code ](https://github.com/Fr0sZ/Skylines-Multiplayer)
* [Road Namer for the road selector tool code ](https://github.com/PropaneDragon/RoadNamer)
