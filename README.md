# Unicode Spoof Guard External Library

## Overview
Unicode Spoof Guard is an OutSystems Developer Cloud (ODC) external library implemented in .NET 8. It provides server actions that help developers detect and mitigate Unicode spoofing techniques—homoglyphs, mixed scripts, invisible characters, and bidirectional control characters—before user-provided data enters critical workflows.

For detailed instructions on how to use the server actions in your OutSystems application, please see the [OutSystems Usage Guide](USAGE.md).

## Project Structure
- `UnicodeSpoofGuard.sln` – Solution file for the project.
- `README.md` – This documentation file.
- `resources/` – Assets kept at the repository root (for documentation or distribution).
- `src/` – Contains all source code for the external library.
  - `UnicodeSpoofGuard.csproj` – Main C# project file.
  - `IUnicodeSpoofGuard.cs` / `UnicodeSpoofGuard.cs` – Interface and implementation exposed to OutSystems.
  - `confusables.txt` – Embedded Unicode Consortium data file.
  - `generate_upload_package.ps1` – PowerShell packaging script.
  - `resources/UnicodeSpoofGuard.png` – Icon embedded in the library for ODC.
  - `Detection/` – Core spoofing detection logic.
  - `Structures/` – Data structures exposed to OutSystems.
- `tests/` – xUnit test project.
  - `UnicodeSpoofGuard.Tests/` – Test project and unit tests.

## Dependencies & Versions
- Target framework: **.NET 8.0**
- NuGet packages:
  - `OutSystems.ExternalLibraries.SDK` – Provides the attributes and glue for exposing code to ODC (server actions, parameters, structures).
- Test framework: `xUnit` (included via test project template).

## Unicode Confusables Logic
### What is `confusables.txt`?
The Unicode Consortium publishes the [confusables.txt](https://www.unicode.org/Public/security/latest/confusables.txt) data file. It maps characters that are visually similar but have different code points—known as homoglyphs. Attackers exploit these differences to create deceptive identifiers such as `pаypal.com` (Cyrillic `а`) or `rn` versus `m`.

### How the Library Uses It
1. `ConfusablesParser` embeds `confusables.txt` as a resource.
2. On the first call to any server action, the parser loads the file once and caches the data.
3. Two dictionaries are produced:
   - **AllConfusablesMap** – includes the full set of mappings (excluding duplicates and pure ASCII self-maps) used for detection.
   - **SingleCharCanonicalMap** – conservative subset containing only single-character-to-single-character mappings used for canonicalization.
4. `SpoofDetector` consumes these dictionaries:
   - Homoglyph detection checks for characters present in `AllConfusablesMap` and reports findings.
   - Canonicalization uses `SingleCharCanonicalMap` to produce a safe baseline string.

### Updating the Data File
1. Download the latest `confusables.txt` from the Unicode Consortium.
2. Replace `src/confusables.txt` with the updated file.
3. Rebuild the project.

### Execution Flow When a Server Action Runs
1. Server action entrypoint (e.g., `IsSafeEmail`) checks for null or empty input.
2. The input passes to `SpoofDetector.Analyze`, which runs multiple detection steps:
   - Homoglyph scan using the cached confusables map.
   - Mixed-script detection using Unicode ranges for Latin, Cyrillic, and Greek.
   - Invisible/format character detection using Unicode categories.
   - Bidirectional control character detection.
3. Findings are returned, and the canonical string is generated with single-character replacements.
4. `AnalysisResult` is populated with:
   - `IsSafe` – `true` when no findings were produced.
   - `Reason` – first finding description (or empty if safe).
   - `CanonicalValue` – normalized version of the input.
   - `Findings` – the complete list of `DetailedFinding` structures.

## Server Actions & Structures
### Structures
#### `AnalysisResult`
- `IsSafe` (bool) – Indicates whether the input passed all checks.
- `Reason` (text) – Short explanation when the input is unsafe; empty when safe.
- `CanonicalValue` (text) – Canonicalized text using conservative replacements.
- `Findings` (List<DetailedFinding>) – Complete list of detected issues.

#### `DetailedFinding`
- `FindingType` (text) – Category name; current values and examples include:
  - **Homoglyph** – A visually deceptive character detected. Example: Cyrillic `і` inside `admіn`.
  - **MixedScript** – Multiple Unicode scripts mixed in the same identifier. Example: Latin letters combined with Greek `α`.
  - **InvisibleCharacter** – Non-printing or format characters such as zero-width space (`\u200B`) or soft hyphen (`\u00AD`).
  - **BidirectionalControl** – Bidirectional control characters that can flip visual order, e.g., RIGHT-TO-LEFT OVERRIDE (`\u202E`).
  - **MultiCharacterConfusable** – Single character that mimics a sequence (e.g., a character confusable with `rn`).
- `Description` (text) – Human-readable explanation.
- `Position` (integer) – Zero-based index of the flagged substring.
- `Substring` (text) – Original substring that triggered the finding.

### Server Actions
| Action | Description | Inputs | Output |
| --- | --- | --- | --- |
| `IsSafeEmail` | Evaluates an email address for Unicode spoofing patterns. | `email` (text) | `AnalysisResult` |
| `IsSafeDomain` | Evaluates a domain or host string for spoofing indicators. | `domain` (text) | `AnalysisResult` |
| `IsSafeUsername` | Checks user identifiers (usernames/handles) for spoofing tactics. | `username` (text) | `AnalysisResult` |
| `GetCanonicalString` | Returns a canonical version of text using safe single-character replacements. | `text` (text) | `CanonicalText` (text) |
| `AnalyzeTextForSpoofing` | Performs deep analysis and returns every finding detected in the text. | `text` (text) | `Findings` (List<DetailedFinding>) |

## Usage Examples
### `IsSafeEmail`
#### Success Scenario
Input: `"support@example.com"`
- `IsSafe` = `true`
- `Reason` = `""`
- `CanonicalValue` = `"support@example.com"`
- `Findings` = `[]`

#### Failure Scenario
Input: `"billing@pаypal.com"` (Cyrillic `а`)
- `IsSafe` = `false`
- `Reason` = `"Character 'а' at position 11 is a homoglyph of 'a'."`
- `CanonicalValue` = `"billing@paypal.com"`
- `Findings` contains a single `Homoglyph` entry for the Cyrillic `а`.

### `IsSafeDomain`
#### Success Scenario
Input: `"secure.company.org"`
- `IsSafe` = `true`
- `CanonicalValue` = `"secure.company.org"`
- `Findings` = `[]`

#### Failure Scenario
Input: `"adm­in.com"` (contains an invisible soft hyphen `\u00AD`)
- `IsSafe` = `false`
- `Reason` references the invisible character.
- `CanonicalValue` removes single-character confusables but retains the structure.
- `Findings` includes an `InvisibleCharacter` entry with position and substring details.

### `IsSafeUsername`
#### Success Scenario
Input: `"admin123"`
- `IsSafe` = `true`
- `Findings` = `[]`

#### Failure Scenario
Input: `"admіn"` (Cyrillic `і`)
- `IsSafe` = `false`
- `Reason` describes the homoglyph.
- `CanonicalValue` = `"admin"`
- `Findings` includes a `Homoglyph` entry at the position of the Cyrillic character.

### `GetCanonicalString`
#### Success Scenario
Input: `"Pаypal"` (Cyrillic `а`)
- Output: `"Paypal"`

#### Failure Scenario
Input: `"support"` (no confusable characters)
- Output: `"support"`
- The text already uses safe characters, so no substitutions are applied.

### `AnalyzeTextForSpoofing`
#### Success Scenario
Input: `"Login screen"`
- Returns an empty list.

#### Multi-Finding Scenario
Input: `"admіnα"` (Cyrillic `і`, Greek `α`)
- Returns two findings:
  1. `Homoglyph` for the Cyrillic `і`.
  2. `MixedScript` reporting Latin + Greek.

## Building & Packaging
1. Restore and build locally by running `dotnet build` from the repository root.
2. Run the packaging script from the `src` directory:
   ```powershell
   cd src
   ./generate_upload_package.ps1
   ```
   This creates the `../.out/ExternalLibrary.zip` file, ready for upload to ODC.

## Running Tests
From the repository root, execute the following command:
```powershell
dotnet test UnicodeSpoofGuard.sln
```
All tests should pass, confirming the library is functioning as expected.

---
Maintained by the Unicode Spoof Guard project team. Contributions and updates to the Unicode data are encouraged to stay aligned with the latest Unicode Consortium recommendations.
