# AnS - Limited Mode (Not Active on Server Side)

# DO NOT USE THIS MODE YET (See Discord for availability)

Allows you to pull up to 2 realms + region data at one time. The lua is built on the server side and thus this version requires very little system memory on your side. It is limited to 2 to prevent excess use of server system memory.

The 2 realms may consist of both US and EU realms. Each will include the proper region data if requested.

If you do not include region data then you may select up to 3 realms.

# AnS - Full Mode - Default Mode

Allows you to pull as many realms as you want, but will build the final LUA data file for WoW locally on your machine. Each realm increases the build time and memory usage. So, be sure you have enough memory locally to support the number of realms you want to pull.

Assume memory cost to build lua will roughly be 3.5x the actual DB file size.

On average with 50-70MB for Stormrage without US region data, it will be about +175-245MB increased memory usage to compile the LUA locally. Including the US region data with Stormrage, then you are looking at about +420-490MB increased memory usage.

# Switching Between Full / Limited

To switch back and forth between the two, just Check / Uncheck the new Full Mode checkbox in the UI.

When checked Full Mode will be used. When unchecked Limited mode will be used.

# It won't download the data on another computer!

The server keeps track of an IP hash for the specified realms requested for both Limited and Full. This is the only way, without some form of login to track usage of downloads, and prevent excess bandwidth usage.

You will need to manually copy the Data.lua file in AnsAuctionData addon folder to the specified computer AnsAuctionData addon folder.

# Purpose

Downloads Auction House Data from the AnsAuctionService server.

# Compatability

Windows 8+, Linux, Mac

# Requirements for Any OS

-   Minimum 100MB RAM
-   Hard Drive Space 150MB+
-   Requires x64 processor

# Windows

See releases on the side for the latest version

# Getting it Working on Linux

-   You will need to build it yourself for Linux.
-   You will need Visual Studio for Linux and .Net Core 3.1 minimum + Avalonia UI 0.9.10+ Package for Visual Studio
-   In the DataSource.cs you will need to overwrite the WoWPath get method to your specific WoW Interface Directory.
-   Use the Build -> Publish Menu Item
-   Configure for linux with Release x64, Self-Contained, Produce Single File, target run time linux-x64

# Getting it Working on Mac

For the moment you will need to build it yourself for Mac, however I do plan to add in prebuilts in the near future.

-   You will need Visual Studio for Mac and .Net Core 3.1 minimum + Avalonia UI 0.9.10+ Package for Visual Studio
-   You will also need access to XCode to finalize the Mac build.
-   Use the Build -> Publish Menu Item
-   Configure for mac with Release x64, Self-Contained, Produce Single File, target run time osx-x64
