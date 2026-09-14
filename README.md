# AetherVPN

<div align="center">

⚡ **Windows VPN Client powered by [Aether Core](https://github.com/CluvexStudio/Aether)**

[![Build](https://github.com/YOUR_USERNAME/AetherVPN/actions/workflows/build.yml/badge.svg)](https://github.com/YOUR_USERNAME/AetherVPN/actions/workflows/build.yml)
[![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)](LICENSE)

</div>

---

## Overview

AetherVPN is a Windows desktop application that provides a graphical user interface for the [Aether](https://github.com/CluvexStudio/Aether) censorship circumvention engine. It wraps the powerful Aether core, making it easy to connect to Cloudflare's WARP network through an encrypted tunnel.

### Features

| Feature | Description |
|---------|-------------|
| 🔗 One-click connect | Start your VPN with a single click |
| 🛡️ MASQUE tunneling | HTTP/3 (QUIC) and HTTP/2 (TLS) for DPI resistance |
| 🔐 WireGuard | Fast, lightweight transport |
| 🔄 Gool mode | Double WireGuard for extra encryption |
| 🪆 MasqueInMasque | Nested MASQUE tunnel with different exit IP |
| 🎭 Traffic obfuscation | Multiple profiles (firewall, balanced, heavy) |
| 🌐 System proxy | Auto-configure Windows system proxy |
| 📊 Real-time logs | Live log viewer with save/clear |
| 🔄 Auto-reconnect | Automatically reconnect on connection drop |
| 💾 Settings persistence | Save your preferred configuration |
| 🖥️ Modern UI | Dark theme, responsive WPF interface |

## Quick Start

### Download

1. Go to [Releases](../../releases) and download the latest `AetherVPN-windows-x86_64.zip`
2. Extract the ZIP file
3. Run `AetherVPN.exe`

### Usage

1. **Select Protocol** — MASQUE is recommended for most networks
2. **Choose Scan Mode** — Balanced is a good default
3. **Click Connect** — Wait for endpoint scanning
4. **Connected!** — Your SOCKS5 proxy is at `127.0.0.1:1819`

### Protocols

| Protocol | Best For | Description |
|----------|----------|-------------|
| **MASQUE** ⭐ | Most networks | Looks like normal HTTPS traffic |
| **WireGuard** | Less restricted networks | Fast and lightweight |
| **Gool** | High censorship | Double WireGuard encryption |
| **MasqueInMasque** | Different exit IP needed | Nested MASQUE tunnel |

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Rust 1.98+](https://rustup.rs/)
- C/C++ compiler (MSVC)
- [CMake](https://cmake.org/)
- [NASM](https://www.nasm.us/)

### Build Steps

```bash
# 1. Clone this repository
git clone https://github.com/YOUR_USERNAME/AetherVPN.git
cd AetherVPN

# 2. Clone Aether core
git clone https://github.com/CluvexStudio/Aether.git ../aether-core

# 3. Build Aether core
cd ../aether-core/aether
cargo build --release --target x86_64-pc-windows-msvc
cd ../../AetherVPN

# 4. Build the GUI
cd src/AetherVPN
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 5. Copy aether.exe next to the published AetherVPN.exe
copy ..\..\..\..\aether-core\aether\target\x86_64-pc-windows-msvc\release\aether.exe publish\
```

### GitHub Actions

The project includes a complete CI/CD pipeline. To use it:

1. Fork this repository
2. Push a version tag: `git tag v1.0.0 && git push --tags`
3. GitHub Actions will automatically build and create a release

Or trigger manually from the **Actions** tab → **Build AetherVPN** → **Run workflow**.

## Architecture

```
┌─────────────────────────┐
│     AetherVPN.exe       │  ← WPF GUI (.NET 8)
│  ┌───────────────────┐  │
│  │  MainViewModel    │  │  ← MVVM pattern
│  │  ┌─────────────┐  │  │
│  │  │ CoreService  │──┼──┼──→ manages process lifecycle
│  │  │ ProxyService │──┼──┼──→ Windows system proxy
│  │  │ LogService   │  │  │  ← captures stdout/stderr
│  │  └─────────────┘  │  │
│  └───────────────────┘  │
└───────────┬─────────────┘
            │ spawns/manages
            ▼
┌─────────────────────────┐
│      aether.exe         │  ← Aether Core (Rust)
│  MASQUE / WireGuard     │
│  SOCKS5 on :1819        │
└─────────────────────────┘
```

## Configuration

Settings are saved to `%APPDATA%\AetherVPN\settings.json`. The Aether identity (WARP device registration) is stored in `%APPDATA%\AetherVPN\aether.toml`.

### Environment Variables

AetherVPN configures these for the Aether core:

| Variable | Description |
|----------|-------------|
| `AETHER_PROTOCOL` | masque, wg, gool, mim |
| `AETHER_SCAN` | balanced, quick, turbo, full |
| `AETHER_IPV` | 4, 6, 46 |
| `AETHER_SOCKS` | SOCKS5 bind address |
| `AETHER_NOIZE` | Obfuscation profile |
| `AETHER_MASQUE_HTTP2` | Enable HTTP/2 mode |
| `AETHER_MASQUE_FRAGMENT` | Enable TLS fragmentation |

## License

This project is licensed under the AGPL-3.0 License, same as the Aether core.

## Credits

- **[Aether Core](https://github.com/CluvexStudio/Aether)** by CluvexStudio — the engine behind AetherVPN
- **[Cloudflare Quiche](https://github.com/cloudflare/quiche)** — QUIC/HTTP3 implementation
