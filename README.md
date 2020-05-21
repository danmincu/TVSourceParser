# TVSourceParser

One click to parse `Romanian television sources` followed by creation with Vagrant of a VM that hosts an Acestream server proxy and loads those channels into VLC

It should work with both `Hyper-V` and `Oracle's VirtualBox`; The VirtualBox solution should work on MacOS/Linux although in Linux the native code could be used.

Prerequisites: 

0. Windows (compile the c# for linux or macos if needed)
1. HyperV or Virtual Box
2. Vagrant
3. VLC 64x (change the path if the 32x is used)

Install: Run the webparser.exe to install and start the TV; for now the batch files are for windows only

Further: look into building a Linux appliance(Raspberry Pi/Beaglebone) with same purpose.

![Channel list](https://github.com/danmincu/TVSourceParser/raw/master/Sample/Screen.png)
