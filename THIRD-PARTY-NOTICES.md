# Third-party components

REV05 uses the following directly referenced NuGet components. Each component remains subject to its own upstream license. The official release pipeline writes the complete transitive package graph and license/provenance fields into `sbom.spdx.json`; that generated SBOM is the release record for the exact resolved build.

| Package | Direct version |
|---|---:|
| coverlet.collector | 6.0.2 |
| HelixToolkit.Geometry | 3.1.2 |
| HelixToolkit.Wpf | 3.1.2 |
| Microsoft.EntityFrameworkCore.Design | 9.0.14 |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.14 |
| Microsoft.EntityFrameworkCore.Tools | 9.0.14 |
| Microsoft.Extensions.Configuration | 9.0.13 |
| Microsoft.Extensions.Configuration.Json | 9.0.13 |
| Microsoft.Extensions.DependencyInjection | 9.0.14 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.14 |
| Microsoft.NET.Test.Sdk | 17.12.0 |
| netDxf | 2023.11.10 |
| PdfPig | 0.1.14 |
| PDFtoImage | 5.2.0 |
| QuestPDF | 2026.6.0 |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.13 |
| Xbim.Essentials | 6.0.587 |
| Xbim.Geometry | 5.1.820 |
| Xbim.Geometry.Engine.Interop | 5.1.820 |
| Xbim.Ifc | 6.0.587 |
| Xbim.ModelGeometry.Scene | 5.1.820 |
| xunit | 2.9.2 |
| xunit.runner.visualstudio | 2.8.2 |

No third-party component is relicensed by CraneOpt. Review the generated SBOM and the package license files included in the NuGet cache/source package before external distribution.
