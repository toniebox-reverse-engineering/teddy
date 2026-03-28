---
description: Build and deploy Linux GUI to Downloads (preserves customTonies.json)
---

Build the Linux x64 GUI release and deploy it to ~/Downloads/teddybench-v1.7.0-linux-x64/, preserving the existing customTonies.json file.

Steps:
1. Backup customTonies.json if it exists
2. Build TeddyBench.Avalonia for linux-x64 (Release, self-contained, single-file)
3. Copy all artifacts to the Downloads directory
4. Restore the customTonies.json backup
5. Set executable permissions on TeddyBench.Avalonia
6. Show deployment summary
