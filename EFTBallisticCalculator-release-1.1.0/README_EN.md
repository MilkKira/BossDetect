# Project Spark: Modern Tactical HUD

A Fire Control System (FCS) and tactical information UI mod designed for SPT.

**What can it do?**

- Predicts the ballistic impact point based on a given distance while aiming down sights.
- Displays data including distance, heading, angle, bullet parameters, estimated muzzle velocity, and time of flight (TOF).
- Shows some very cool~ stuff on your screen, like wind speed, wind direction, temperature, and atmospheric pressure. Of course, they are all entirely fake :P
- Monitors various vital information and data in real-time and displays them seamlessly on your screen.
- Includes weapon status, available throwables, biometric health status, active buff/debuff effects, and a squad status brief.

**How to install?**

- Download the latest release from the Releases section on the right. Extract the archive and simply drop the `BepInEx` folder into your game's root directory, overwriting if necessary.

**Compatibility?**

- **Fika:** Fully compatible. I haven't encountered any issues during my actual testing.
- **Fika.Co-op:** Unsure/Untested. I don't have the environment to test this. The FCS relies on the client's Camera system to function; hopefully, BSG isn't rendering other players' cameras.
- **Fika.Headless:** Unsure/Untested. Same as above.
- **Realism:** They are definitely incompatible now. This project is built on SPT 4.0.13, while Realism is currently on 3.11.4. I extracted the ballistic algorithm and rebuilt it as utility methods, but Realism likely uses its own ballistic parameters. Furthermore, `GClass` obfuscation changes with every BSG version update.

**Usage Guide**

- Upon entering a raid, various information panels will appear on your screen. The left side houses the FCS status, environment sensors (all environmental data except time and location are simulated), and squad status. The right side displays health and active buffs (invisible when no effects are active). The top shows the weapon panel and throwables.
- The FCS panel will display current distance, incline/elevation angle, cant angle, bullet parameters, muzzle velocity, and TOF.
- Press the **T** key to set your crosshair position as the target. The FCS will automatically measure the horizontal distance to the point, calculate the fire control data, and predict the impact point.
- The predicted impact point is represented by a glowing yellow dot, which is visible while aiming down sights (ADS).
- If the crosshair is aimed at an invalid target (such as the sky), the distance data will automatically clear.
- Press the **Backspace** key to manually clear the fire control data.
- By holding **Ctrl, Alt, or Shift** along with the **Up/Down Arrow keys**, you can manually input the distance or calibrate/adjust the current data.
- The impact prediction dot remains visible even if you toggle off all HUD elements. However, since I didn't set up a standalone distance display, I highly recommend keeping at least the FCS panel active to accurately judge your engagement distance.

**Special Thanks**

To koloskovnick and his MedEffectsHUD. I referenced the code used for fetching and updating buff effects. The original project uses the MIT license, and I would like to credit them here.

[MedEffectsHUD](https://github.com/KoloskovNikolay/MedEffectsHUD)
