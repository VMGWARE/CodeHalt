[![License: CC BY-NC-SA 4.0][license-shield]][license-url]
[![Contributors][contributors-shield]][contributors-url]
[![Issues][issues-shield]][issues-url]
[![Forks][forks-shield]][forks-url]

# CodeHalt

 CodeHalt - A simple process manager

## Description

CodeHalt is a simple process manager that allows you to manage processes that are running in the background and stop some or all of them. It is designed to be simple and easy to use. It is also designed to be as lightweight as possible.

## Getting Started

### Dependencies

* Windows 10
* .NET 6.0

### Installing

* Download the latest release from the releases page
* Extract the zip file
* Run CodeHalt.exe

### Executing program

Run CodeHalt.exe either as an administrator or as a normal user. If you run it as a normal user, you will not be able to terminate processes that are running as an administrator.

### Features

* Automatically creates a shortcut to CodeHalt.exe in the Programs folder
* Will Generate a complete log of all processes that are terminated or other events that occur
* The targeted processes are stored in a `processes.txt` and can be edited manually during runtime
* Easy to use interface

### Interface

* Scan Processes - List all processes that are marked to be managed by CodeHalt
* Open In Explorer - Open the process/logs directory in explorer
* Modes
  * Passive - CodeHalt will not do anything to the process
  * Active - CodeHalt will automatically halt the process if it is running

* Termintate Selected - Terminate the selected process
* Terminate All - Terminate all processes that are marked to be managed by CodeHalt

## Help

If you need help, you can either open an issue on the GitHub page or you can join the Discord server [https://discord.gg/xhnmM4zgmu](https://discord.gg/xhnmM4zgmu).

## License

Distributed under the `CC BY-NC-SA 4.0` License. See [LICENSE](LICENSE) for more information.

## Contributors

<a href = "https://github.com/Codycody31/CodeHalt/graphs/contributors">
<img src = "https://contrib.rocks/image?repo=Codycody31/CodeHalt"/>
</a>

[contributors-shield]: https://img.shields.io/github/contributors/Codycody31/CodeHalt.svg?style=for-the-badge
[contributors-url]: https://github.com/Codycody31/CodeHalt/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/Codycody31/CodeHalt.svg?style=for-the-badge
[forks-url]: https://github.com/Codycody31/CodeHalt/network
[issues-shield]: https://img.shields.io/github/issues/Codycody31/CodeHalt.svg?style=for-the-badge
[issues-url]: https://github.com/Codycody31/CodeHalt/issues
[license-shield]: https://img.shields.io/badge/License-CC_BY--NC--SA_4.0-lightgrey.svg?style=for-the-badge
[license-url]: https://creativecommons.org/licenses/by-nc-sa/4.0/
