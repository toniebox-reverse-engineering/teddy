# Tonie file tool

With this tool you can dump existing files of the famous audio box or create custom ones.

# Linux
WARNING: The `TeddyBench` Linux port has alpha status. So don't use it unless you are a developer and you are able to understand cryptic error messages. It is not fully tested but converting files should work.

It is tested with Ubuntu 20.04.

## Requirements
Please install the following packages

```
# sudo apt install mono-complete xcb ffmpeg libgdiplus libcanberra-gtk-module libcanberra-gtk3-module
```

## Running

### General

You have to run `TeddyBench` with `mono`. Using `wine` is not working.

```
# mono TeddyBench.exe
```

### Know issues
For some reasons TonieBench is running unstable with mono. So you can try the following steps.

**A. Running with sudo (root)**

```
# sudo mono TeddyBench.exe
```

**B. Set some environment variables**

```
# export MONO_MANAGED_WATCHER=disabled
# export MONO_WINFORMS_XIM_STYLE=disabled
# mono TeddyBench.exe
```
## Development
You can compile it directly under Linux with `monodevelop`.
