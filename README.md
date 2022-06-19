# MRTK_ToFAR
Mixed Reality Toolkit (MRTK) HandInteraction examples with "ToF AR" hand tracking

<img width="957" alt="title" src="https://user-images.githubusercontent.com/530182/174469113-fbfddd0d-3d10-465d-8c26-5a8afd8a4b86.png">

"ToF AR" is a xR SDK for integrated LiDAR/ToF camera phones released by Sony, that includes high accuracy hand tracking.  
https://developer.sony.com/develop/tof-ar/

"Mixed Reality Toolkit" is a xR SDK for HoloLens, smart phones and other xR devices, that includes nice UI compornents for xR.  
https://github.com/Microsoft/MixedRealityToolkit-Unity

This sample enables ToF AR hand tracking in MRTK.

Video: https://twitter.com/peugeot106s16/status/1538015779547136005

## Environment
* Rear ToF camera integrated phones (Recommend iToF devices, LiDAR depth not enough to fine hand tracking)  
https://developer.sony.com/develop/tof-ar/development-guides/docs/ToF_AR_User_Manual_en.html#_list_of_sdk_compatible_devices

* Unity 2020.3.x
* ToF AR v1.0.0
* MRTK v2.8.0
* ARFoundation

## How to Build

1. Download "ToF AR" from Sony Developer World.
2. Open project with Unity Editor.
3. Skip setup for MRTK.  
![skip_mrtk_setup](https://user-images.githubusercontent.com/530182/174471018-857cf705-c61f-4698-b33f-e87e7d6b28b7.png)
5. Import following 5 "ToF AR" unitypackage files.  
![tofar_packages](https://user-images.githubusercontent.com/530182/174471031-f46b95df-6a1b-4ddc-b0e1-385468e82fea.png)
7. Switch platform to Android or iOS in BuildSettings dialog.
8. Build and deploy to device.

* If prompted setup "TextMesh Pro", setup it.  
![setup_tmp](https://user-images.githubusercontent.com/530182/174471055-fe89e84a-f7fb-47c4-8e92-78df3c5214b4.png)

## Lisence

This project is licensed under the MIT License, see the LICENSE.txt file for details
