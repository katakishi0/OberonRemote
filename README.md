<div align="center">
  <img src="Oberon.Remote.Extended/Assets/oberon-extended.svg" alt="Oberon Extended logo" width="104">
  <h1>Oberon Remote Extended</h1>
  <p>Use your favorite controller on Xbox through your Windows PC.</p>
  <p>A community fork of <a href="https://github.com/SamsidParty/OberonRemote">Oberon Remote Input</a>.</p>
  <p><a href="https://github.com/katakishi0/OberonRemote/releases"><strong>Download Oberon Extended</strong></a></p>
</div>

---

## What does it do?

Oberon Extended lets you connect a supported game controller to your Windows PC and use it with the original Oberon Remote Input app on your Xbox. It has a simple connection screen, customizable controller settings, and several color themes.

You will need a Windows PC, the original Oberon app on your Xbox, and a shared home network.

## Download and install

Visit the **[Releases page](https://github.com/katakishi0/OberonRemote/releases)**. Choose whichever version works best for you:

| Windows download | Who is it for? |
| --- | --- |
| **Oberon Extended Setup (.exe)** | Recommended for most people. Installs the app, provides a Start Menu shortcut, and includes an uninstaller. |
| **Oberon Extended Portable (.zip)** | Run without installing. Unzip everything into a folder and open `Oberon.Extended.exe`. |

If the Releases page is empty, there is no public release yet. Development builds, including the installer and portable version, can be found under [GitHub Actions](https://github.com/katakishi0/OberonRemote/actions/workflows/oberon-extended.yml) after successful runs. These are preview builds.

### Connect in a few steps

1. Open Oberon Extended on your PC.
2. Connect a supported Xbox, PlayStation, Nintendo Switch, or other PC game controller using USB or Bluetooth.
3. On the Xbox, open the original Oberon Remote Input app.
4. Enter the PC address shown on the Oberon Extended home screen.
5. Connect. Both devices must be on the same home network.

If Windows asks whether to allow network access, choose your private home network. You can minimize Oberon Extended while playing.

## Main features

| Feature | What it means |
| --- | --- |
| **Easy connections** | View your PC address and use one button to connect or disconnect the server. |
| **Controller support** | Works with a variety of controllers recognized by Windows, SDL, or XInput. |
| **Controller status** | See buttons, analog sticks, connection status, and optional battery information. |
| **SDL and XInput** | Switch between input systems for Xbox-compatible controllers. If SDL cannot detect a connected XInput controller, Extended can offer to switch. |
| **Keyboard and mouse** | Under Experimental, you can enable an optional virtual controller, customize the keys, and arm it with F8. |
| **Appearance** | Choose green, dark, light, and other themes; change accent colors; use clean rectangular buttons. |
| **Personal settings** | Adjust stick deadzones, vibration, controls, and what appears on the dashboard. |
| **Help** | Find a step-by-step connection guide and explanations for advanced features inside the app. |
| **Updates** | Check GitHub Releases from inside Oberon Extended. |

### If your Xbox controller is not detected

Xbox-compatible controllers may work better with **XInput** than SDL. Open **Experimental** to change the input mode. When Extended finds a compatible XInput device that SDL isn't recognizing, it can also suggest switching. You decide whether to apply it, keep SDL, or hide the suggestion permanently.

Third-party controllers may not report their battery level. An unknown battery status does not mean that the batteries are empty.

### Keyboard and mouse controls

Go to **Experimental** to enable the virtual keyboard and mouse controller. Select **Customize keys** to assign a different keyboard key to each gamepad action. You can restore the original layout whenever you want.

Press **F8** to turn virtual input on or off. It is disabled by default, so it will not send gamepad commands while you are typing normally unless you explicitly enable and arm it.

### Themes and layout

The interface supports multiple palettes, including Xbox Green and Xbox Light. Extra dashboard information can be hidden from **Settings**, including status details you do not need.

## Troubleshooting

**Can't connect to your Xbox?** Check that both devices share the same home network. If your firewall asks for permission, allow Oberon Extended on private networks.

**Controller missing?** Make sure it appears in Windows, then try the Xbox-compatible XInput option or check **Diagnostics**.

**Want to start over?** Go to **Settings** and choose **Restore default settings**. This resets your saved preferences and the app can be reopened with its defaults.

## Credits and license

- **Original project:** [SamsidParty](https://github.com/SamsidParty/OberonRemote) and contributors, including [SamarthMP](https://github.com/SamarthMP).
- **Extended project maintenance:** [katakishi0](https://github.com/katakishi0).
- **Libraries:** [SDL2](https://libsdl.org/), [MonoGame](https://monogame.net/), [Fleck](https://github.com/statianzo/Fleck), .NET and Windows Forms.

The original [Oberon license](LICENSE) remains in effect. Source and attribution requirements and the original Microsoft Store distribution restriction also apply. Third-party license information is preserved in [Legal/THIRD-PARTY-NOTICES.txt](Legal/THIRD-PARTY-NOTICES.txt).

Extended Windows source is available in [Oberon.Remote.Extended](Oberon.Remote.Extended/) and the original Xbox project in [Oberon](Oberon/). Development builds are built and checked using GitHub Actions.
