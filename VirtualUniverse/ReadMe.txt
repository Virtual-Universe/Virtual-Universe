Universe-Dev 0.9.3+ (git)
Rowan Deppeler <greythane@gmail.com>
June 2014
Fly-Man-
Jan 2015
===============================================

** Simplified startup scripts **
================================
Easy to use startup scripts are provided to simplify running Universe.
Choose the appropriate one(s) for your system and configuration.

 *nix flavours (including Mac)
-------------------------------
All these scripts are intended to be executed from a terminal window.

The following are the general startup scripts for use in testing, maintenance etc...
sim_console.sh       			: Start only the Universe standalone server
grid_console.sh      			: Start only the Universe grid server


Note:
 These 'background' modes require the 'screen' program installed on your system.
   Linux (ubuntu variations) >> apt-get install screen;
   Mac >> already available
  
run_standalonemode.sh       	: Run Universe standalone mode in background
run_gridmode.sh             	: Run Universe grid mode in background


GUI scripts for Windows and Mac

-------------------------------

The same commands are available as gui scripts, <double_click> to execute

sim_console.command				: Mac
sim_console.bat					: Windows
grid_console.command			: Mac
grid_console.bat				: Windows


** The default configuration **
===============================
The default configuration of Universe is setup to run in Standalone mode and
to use an SQLite database, with no pre-configured users or regions.
On initial startup, you will be asked to create your initial region, together with 
your first user.

** Quick Customising **
=======================
You can quickly set a few options to customise your Universe installation.
To do this, modify the commented out settings in 'MyWorld.ini' located in the
 'Config' folder.  This will allow you to set the name of your Sim, configure your default
  region and specify an address to use if you do not want to use your external IP.


** Grid mode **
===============
This configuration has been setup to run as a standalone simulator. If you wish to re-configure
and use the Grid mode of operation, change the selected include mode in the 'Main.ini' file.

Edit Main.ini file in
Config > Sim > Main.ini

Comment the "Include-Standalone =" line.
Uncomment the "Include-Grid =" line.

Save and re-start.
Note:  You will need to use both the 'grid' and 'sim' startup scripts.

** Updating **
==============
Checkout the 'Build Your Own.txt' file in the 'UniverseDocs' folder for details
 if you want to build from source.
Re-compile and copy/paste the new 'UniverseSim/bin' subdirectory from your build environment.

Monthly 'Development' build snapshots are available at the following links:

Windows
https://drive.google.com/file/d/0B2u55gI751a8VXJBckZJWU5rZ1E/edit?usp=sharing

Mono 32 bit  (Linux / Mac)
https://drive.google.com/file/d/0B2u55gI751a8OEgtV0Q0Yk4wWEE/edit?usp=sharing

Mono 64bit
https://drive.google.com/file/d/0B2u55gI751a8ZmV1OEE4ZDE4Nm8/edit?usp=sharing

Download your desired update snapshot.
Delete or backup the existing 'UniverseSim/bin' subdirectory.
Extract the update package and copy the resulting 'bin' folder to your 'UniverseSim' folder.
re-start..

Questions?
==========
Checkout the #universe-support irc channel on freenode,
or check into the Google+ AuroraSim/UniverseSim group at 
https://plus.google.com/communitites/113034607546142208907

Rowan Deppeler
<greythane @ gmail.com>

June 2014
=======================

For licensing information, please see the relevant licenses.
