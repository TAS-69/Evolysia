# Third-party notices

Evolysia uses Godot Engine and .NET. Their licences are independent of Evolysia's source and artwork licences.

## Godot Engine

Godot Engine is distributed under the MIT licence. Copyright belongs to Juan Linietsky, Ariel Manzur and the Godot Engine contributors as stated in the engine's licence.

[Godot licence and third-party notices](https://godotengine.org/license/).

The Windows package includes `godot-notices.json`, extracted from the exact engine used for that build. It contains the engine's complete licence, component copyright notices and bundled dependency licences. Replacing the engine's startup logo does not change these notices or obligations.

## .NET

The self-contained Windows build includes the .NET runtime and Godot's managed assemblies. The package retains their accompanying licence and notice files. [.NET runtime licence](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) and [third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT).

## Development dependencies

The source build uses Microsoft.NET.Test.Sdk, xUnit, its Visual Studio runner and coverlet.collector for tests. The artwork tools use Pillow and NumPy. These development dependencies retain their own package licences and are not bundled as game runtime tools.

## Optional local models

No language-model weights or llama.cpp binaries are included in the game download. The optional model lab can download them separately. Each runtime and model has its own licence; review its upstream terms before downloading, modifying or redistributing it.

## Fonts

The current client uses Godot's bundled default UI font, covered by the engine's component notices. Pixelify Sans and Alegreya are named in the art specification for future integration; they are not bundled in this alpha.
