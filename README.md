# UnrestrictedAgeAdvances

An example mod for Millennia that allows to proceed from alternate ages to alternate ages.


## Installation

1. install [BePinEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html) for Millenia
2. drop ``UnrestrictedAgeAdvances.dll`` into ``Millenia/BePinEx/plugins``


## Configuration

This mod has various options that can be set by editing its plugin config.
The config gets created during the first execution of the game with the plugin installed, and can be modified afterwards.
The config file can be found at ``Millennia/BepInEx/config/UnrestrictedAgeAdvances.cfg``

### Options:

**AllowAlternateAgeAdvance**

*Allow advancing from alternate ages to alternate ages.*


Default: true


**RemoveAgeAdvanceRequirements**

*Remove the Requirements to advance into a specific age.*
- *0 - disabled*
- *1 - remove requirements of variant and victory ages*
- *2 - remove resitrictions for all ages (disables crisis age lock)*

Default: 0


**DisableCrisisAgeLock**

*Disables age locking caused by triggering crisis conditions*

Default: false



## Build

1. clone/download this repo
2. create a new folder in it called lib
3. copy Assembly-CSharp.dll into lib
4. build
