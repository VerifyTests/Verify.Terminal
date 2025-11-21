# Verify.Terminal

A dotnet tool for managing Verify snapshots.  
Inspired by the awesome [Insta](https://github.com/mitsuhiko/insta) crate.

![A screenshot of Verify.Terminal](res/screenshot.png)

## Installation

Install by running the following command:

```bash
dotnet tool install -g verify.tool
```

## Review pending snapshots

```
USAGE:
    verify review [OPTIONS]

OPTIONS:
    -h, --help                    Prints help information
    -w, --work <DIRECTORY>        The working directory to use
    -c, --context <LINE-COUNT>    The number of context lines to show. Defaults to 2
```

```
> dotnet verify review
```

## Accept all pending snapshots

```
USAGE:
    verify accept [OPTIONS]

OPTIONS:
    -h, --help                Prints help information
    -w, --work <DIRECTORY>    The working directory to use
    -y, --yes                 Confirm all prompts.
```

```
> dotnet verify accept
```

## Reject all pending snapshots

```
USAGE:
    verify reject [OPTIONS]

OPTIONS:
    -h, --help                Prints help information
    -w, --work <DIRECTORY>    The working directory to use
    -y, --yes                 Confirm all prompts.
```

```
> dotnet verify reject
```

## Building

```
> dotnet build.cs
```
