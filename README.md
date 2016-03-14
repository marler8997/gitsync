# What is gitsync?

gitsync is a command line tool that synchronizes files in multiple git repositories.  The settings are managed by a single file called '.gitsync' at the root or your git repository.  Here's an example

```
SourceRepo MyLibrary
Sync inc/interface.h external/inc
```

This .gitsync file indicates that you pull files from another repo called 'MyLibrary'.  The 'Sync' command indicates that the 'interface.h' file should be identical to a file in this repository located at 'external/inc'.


# Dependencies
This repository is dependent on the .NET More library hosted at https://github.com/marler8997/More.  Before opening the GitSync.sln file, clone the More library into the same directory that the gitsync repository exists.  So your file system should have these 3 directories:

  1. _mypath_
  2. _mypath_/gitsync
  3. _mypath_/More
