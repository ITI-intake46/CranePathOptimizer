using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Interfaces;

namespace CraneOptimizer.AI.Services
{
    /// <summary>
    /// Provides the local AI copilot with an explicit product map: framework, tabs,
    /// visible buttons, workflows, and safety boundaries. Keeping this context in code
    /// prevents the LLM layer from guessing about UI features that do not exist.
    /// </summary>
    public sealed class StaticAiSoftwareContextService : IAiSoftwareContextService
    {
        private static readonly AiSoftwareContextDto Context = new()
        {
            Framework = new AiFrameworkInfoDto
            {
                ApplicationName = "CranePathOptimizer / IFC Building Analyzer — Crane Planner Edition",
                ProductPurpose = "A Windows desktop application for BIM/IFC model viewing, mobile crane setup, site zoning, GA-based crane positioning, diagnostics, and reporting.",
                UiFramework = "WPF desktop application using MVVM-style feature folders, XAML views, commands, and Microsoft.Extensions.DependencyInjection.",
                TargetFramework = "net8.0-windows",
                Architecture = "Clean/layered architecture: Domain contains core entities and rules; Application contains services, optimization and reports; Infrastructure contains persistence and IFC/report integrations; CraneOptimizer.AI contains AI contracts, deterministic advisor logic, Ollama providers, prompt/document services, AI views, and AI view models; UI contains the WPF shell plus thin AI host adapters.",
                MajorTechnologies = new[]
                {
                    "WPF",
                    "HelixToolkit.Wpf for 3D scene rendering",
                    "XBIM for IFC/BIM data",
                    "Entity Framework Core with SQLite project/catalog databases",
                    "netDxf for DXF import",
                    "QuestPDF and CSV/Excel exporters for reports",
                    "Genetic Algorithm crane placement engine",
                    "Local Ollama HTTP API for optional natural-language AI responses"
                },
                ArchitecturalLayers = new[]
                {
                    "CraneOptimizer.Domain",
                    "CraneOptimizer.Application",
                    "CraneOptimizer.Infrastructure",
                    "CraneOptimizer.AI",
                    "CraneOptimizer.UI",
                    "CraneOptimizer.Application.Tests",
                    "CraneOptimizer.GAExperiment"
                },
                AiIntegrationMode = "Local-first. The isolated CraneOptimizer.AI project owns deterministic advisor rules, prompt/document services, AI view models/views, and local Ollama providers. The UI host supplies live workflow context through adapter interfaces only."
            },
            Tabs = new[]
            {
                new AiFeatureTabDto
                {
                    TabName = "HOME",
                    Purpose = "Project lifecycle and quick access to common first steps.",
                    PrimaryView = "Features/F10_ProjectManagement/Views/HomeTabView.xaml",
                    PrimaryViewModel = "Shell/MainViewModel and ProjectManagementViewModel services",
                    Actions = new[]
                    {
                        Action("New", "NewProjectCommand", "Create a new crane planning project.", "Available from the HOME project group.", "Confirm project folder and saved database location before relying on project persistence."),
                        Action("Open", "OpenProjectCommand", "Open an existing .craneopt project.", "Available from the HOME project group.", "Use trusted project files only."),
                        Action("Save", "SaveProjectCommand", "Save the current project state.", "Available when project state is loaded.", "Save before long optimization or report-generation workflows."),
                        Action("Load IFC Model", "IfcViewer.LoadIfcCommand", "Import the primary BIM/IFC model into the viewer.", "Available from HOME quick access and IFC MODEL tab.", "Check model units and coordinate alignment after import."),
                        Action("Add Crane", "AddMobileCraneCommand", "Start adding a mobile crane to the 3D scene.", "Quick access route to the CRANES workflow.", "Crane placement must still be validated against site constraints and manufacturer data."),
                        Action("Draw Zones", "StartDrawingCommand", "Start drawing site zones used by placement constraints.", "Quick access route to the ZONES workflow.", "Allowed/forbidden zones strongly affect GA candidate points.")
                    },
                    TypicalQuestions = new[]
                    {
                        "How do I start a new project?",
                        "Where do I load an IFC model?",
                        "What should I do first after opening the application?"
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "IFC MODEL",
                    Purpose = "Import, merge, move, delete, display, filter, and inspect BIM/IFC elements.",
                    PrimaryView = "Features/F2_IfcViewer/Views/IfcTabView.xaml",
                    PrimaryViewModel = "IfcViewerViewModel and ElementManagementViewModel via MainViewModel",
                    Actions = new[]
                    {
                        Action("Load IFC", "IfcViewer.LoadIfcCommand", "Load an IFC model from disk.", "Use when no IFC has been loaded or when replacing the active model.", "Confirm units, geometry, and element counts."),
                        Action("Merge IFC", "IfcViewer.MergeIfcCommand", "Merge another IFC model into the current scene.", "Use for multi-model projects.", "Merged models may need coordinate alignment."),
                        Action("Move IFC", "IfcViewer.MoveIfcCommand", "Move the IFC model in the scene.", "Use when BIM geometry is offset from site/zones/DXF.", "Coordinate changes affect all downstream crane reach calculations."),
                        Action("Delete IFC", "IfcViewer.DeleteIfcCommand", "Remove the loaded IFC model.", "Use when replacing or cleaning the scene.", "Deleting the model invalidates optimization context that depends on elements."),
                        Action("Toggle COG Markers", "UI toggle", "Show or hide element center-of-gravity markers.", "Useful for inspecting lifting points.", "COG visibility does not change calculations."),
                        Action("Color by Weight", "UI toggle", "Color elements by estimated/imported weight.", "Useful for visually finding heavy elements.", "Verify weight provenance before using results for decisions."),
                        Action("Export Element List", "UI action", "Export loaded element metadata.", "Useful before reports or QA.", "Review missing or estimated weights."),
                        Action("Filter", "ElementManagement.ShowFilterDialogCommand", "Open the element filter dialog.", "Use to control which elements are included or shown.", "Filters can change optimization scope."),
                        Action("Upload DXF", "Dxf.UploadDxfCommand", "Load a DXF site overlay from the IFC tab.", "Shortcut to DXF workflow.", "Coordinate alignment matters."),
                        Action("Align DXF", "Dxf.AlignCommand", "Align the DXF overlay with the BIM/site.", "Use after uploading DXF.", "Incorrect DXF alignment can invalidate zones.")
                    },
                    TypicalQuestions = new[]
                    {
                        "How do I load or merge IFC files?",
                        "Why are some elements missing from optimization?",
                        "Where can I filter elements?",
                        "How do I check element weights?"
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "DXF",
                    Purpose = "Import and align DXF site overlays and derive zones from DXF layers.",
                    PrimaryView = "Features/F11_DXF/Views/DXFTabView.xaml",
                    PrimaryViewModel = "Dxf-related view models exposed through MainViewModel",
                    Actions = new[]
                    {
                        Action("Upload DXF", "Dxf.UploadDxfCommand", "Select a DXF file from disk.", "Available in DXF FILES group.", "Use trusted DXF files and check scale/units."),
                        Action("Add Another", "Coming soon", "Placeholder for future multi-DXF import.", "Currently marked as coming soon.", "Do not tell users this is fully implemented."),
                        Action("Align", "Dxf.AlignCommand", "Open alignment dialog to position the DXF on the site.", "Use after upload.", "Alignment affects zone geometry and crane candidate regions."),
                        Action("By Layer", "Dxf.DrawZonesByLayerCommand", "Create zones from closed DXF polylines on selected layers.", "Useful when site zones are already drafted in CAD.", "Confirm each imported zone type before optimization."),
                        Action("Use DXF Colors", "Dxf.UseOriginalColors", "Render DXF overlay with original entity/layer colors.", "Display aid only.", "Does not alter geometry or optimization."),
                        Action("Select / Select All / Delete", "Coming soon", "DXF entity selection/editing placeholders.", "Currently marked as coming soon.", "Do not promise completed behavior for these controls.")
                    },
                    TypicalQuestions = new[]
                    {
                        "How do I import site boundaries from DXF?",
                        "How do I align the DXF with the IFC?",
                        "Can the app create zones from DXF layers?"
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "CRANES",
                    Purpose = "Add, manage, configure, and validate crane data and load charts.",
                    PrimaryView = "Features/F3_CraneSetup/Views/CranesTabView.xaml",
                    PrimaryViewModel = "Crane setup view models exposed through MainViewModel",
                    Actions = new[]
                    {
                        Action("MOBILE CRANE", "AddMobileCraneCommand", "Place a mobile crane in the scene.", "Use before manual crane setup or optimization.", "Placement must respect site zones, outriggers, and working radius."),
                        Action("Remove Selected Crane", "RemoveSelectedCraneCommand", "Remove the selected crane from the scene.", "Use when changing crane selection.", "Removing a crane can invalidate previous lifting result visuals."),
                        Action("Remove All Cranes", "RemoveAllCranesCommand", "Clear all crane instances.", "Use for reset workflows.", "Previous optimization assumptions may no longer apply."),
                        Action("Unlock Position", "CraneSetup.UnlockPositionCommand", "Unlock a fixed crane position for editing.", "Use when manual placement needs adjustment.", "Re-run checks after moving a crane."),
                        Action("Show Swing Radius", "UI toggle", "Display crane swing/radius visualization.", "Visual aid for reach inspection.", "Visualization is not a substitute for load chart validation."),
                        Action("Run Optimizer", "OptimizeCommand", "Launch the GA crane placement optimizer.", "Also available in ANALYZE tab.", "Review inputs, zones, crane model, and load chart before accepting results."),
                        Action("Import Load Chart...", "ImportLoadChart_Click", "Import manufacturer/load-chart capacity data.", "Use before capacity-based optimization.", "Validate units, boom/radius rows, operating condition, and number of lines."),
                        Action("View Saved Charts", "ViewSavedCharts_Click", "Inspect load charts saved in the catalog/database.", "Use for QA and reuse.", "Do not assume saved charts are correct without provenance checks."),
                        Action("Crane Catalog...", "CraneCatalog_Click", "Open crane catalog browsing/management.", "Use to review crane models and specs.", "Catalog values must match manufacturer data used by the project.")
                    },
                    TypicalQuestions = new[]
                    {
                        "Where do I import a load chart?",
                        "How do I add or remove a crane?",
                        "Where do I run the optimizer from?",
                        "How do I verify crane catalog data?"
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "ZONES",
                    Purpose = "Draw and manage allowed zones, forbidden zones, and measurements used by placement constraints.",
                    PrimaryView = "Features/F7_Zones/Views/ZonesTabView.xaml",
                    PrimaryViewModel = "Zone drawing/list view models exposed through MainViewModel",
                    Actions = new[]
                    {
                        Action("Allowed Zone", "StartAllowedCommand", "Start drawing an allowed crane placement area.", "Use to define where cranes may be positioned.", "A missing or too-small allowed zone can make candidate generation fail."),
                        Action("Forbidden Zone", "StartForbiddenCommand", "Start drawing an exclusion area.", "Use for obstacles, no-go zones, protected areas, or unavailable ground.", "Overly broad forbidden zones can eliminate valid setup points."),
                        Action("Measure", "StartMeasureCommand", "Measure distances in the scene.", "Use for quick layout checks.", "Measurement display is advisory; verify project coordinate units."),
                        Action("Finish Zone", "FinishCommand", "Complete the current zone polygon.", "Use after placing vertices.", "Closed geometry is required for reliable zone constraints."),
                        Action("Undo Vertex", "UndoCommand", "Remove the last drawn zone vertex.", "Use during drawing corrections.", "Check final polygon shape before optimization."),
                        Action("Cancel", "CancelCommand", "Cancel the current drawing operation.", "Use when abandoning a zone draw.", "Canceled zones will not constrain optimization."),
                        Action("Delete Selected", "DeleteZoneCommand", "Delete selected zone.", "Use in MANAGE group.", "Deleting zones changes placement constraints."),
                        Action("Delete All", "DeleteAllCommand", "Delete all zones.", "Use carefully for reset.", "Optimization without zones may produce invalid site placements."),
                        Action("Export JSON", "ZoneList.ExportZonesCommand", "Export zones as JSON.", "Useful for audit or reuse.", "Keep exports with project records.")
                    },
                    TypicalQuestions = new[]
                    {
                        "What is the difference between allowed and forbidden zones?",
                        "Why does the optimizer have no valid candidate points?",
                        "How do I export zone geometry?"
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "ANALYZE",
                    Purpose = "Run GA optimization and inspect result/diagnostic commands.",
                    PrimaryView = "Features/F8_GAOptimizer/Views/AnalyzeTabView.xaml",
                    PrimaryViewModel = "CranePlacementViewModel",
                    Actions = new[]
                    {
                        Action("Run Optimizer", "OptimizeCommand", "Open/run the crane placement optimization workflow.", "Use after IFC, crane, load chart, and zones are ready.", "GA output is only as valid as input model, zones, crane specs, and load charts."),
                        Action("Export Analysis", "UI action", "Export analysis information.", "Use after results are available.", "Review output for completeness."),
                        Action("Export Zones (JSON)", "UI action", "Export site zones as JSON from analysis area.", "Use for audit and debugging.", "Zone exports should match the active project state."),
                        Action("GA Diagnostics", "ShowDiagnosticsCommand", "Open GA convergence/generation diagnostics.", "Use after an optimizer run.", "Diagnostics help distinguish convergence issues from input feasibility issues."),
                        Action("View Results", "ShowResultsCommand", "Open optimization result window.", "Use after an optimizer run.", "Confirm lifted/unresolved elements and crane positions before reporting.")
                    },
                    TypicalQuestions = new[]
                    {
                        "How do I run the optimizer?",
                        "Where do I see GA diagnostics?",
                        "How do I interpret low coverage?",
                        "Where can I view optimization results?"
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "REPORTS",
                    Purpose = "Generate user-facing project, element, lifting, crane, and diagnostic reports.",
                    PrimaryView = "Features/F12_Reports/Views/ReportsTabView.xaml",
                    PrimaryViewModel = "CranePlacementViewModel and report dialog services",
                    Actions = new[]
                    {
                        Action("All Elements List", "OpenAllElementsReportCommand", "Build/export a report of elements.", "Use after IFC import and filtering.", "Confirm whether weights are imported, estimated, or missing."),
                        Action("Lifting Summary Report", "OpenLiftingSummaryReportCommand", "Build/export lifting result summary.", "Use after optimization.", "Report is advisory and must be checked by qualified personnel."),
                        Action("All Cranes in Database", "AllCranesInDatabase_Click", "Report catalog cranes and key specs.", "Use for catalog review.", "Verify catalog provenance."),
                        Action("Compare 2 Cranes", "Compare2Cranes_Click", "Compare two crane models/specs and likely coverage.", "Use for alternative selection.", "Comparison depends on current project/model assumptions."),
                        Action("Project Overview", "ProjectOverview_Click", "Report project metadata and imported IFC files.", "Use for documentation.", "Check project paths before sharing."),
                        Action("Site Zones Report", "SiteZonesReport_Click", "Report site zones and vertex coordinates.", "Use for QA and audit.", "Coordinates depend on project coordinate setup."),
                        Action("GA Diagnostics", "ShowDiagnosticsCommand", "Open diagnostics from the report tab.", "Use after optimization.", "Interpret diagnostics alongside failure categories."),
                        Action("View Results", "ShowResultsCommand", "Open optimization results from the report tab.", "Use after optimization.", "Do not report stale results after changing inputs.")
                    },
                    TypicalQuestions = new[]
                    {
                        "Which report should I generate for my supervisor?",
                        "Where is the lifting summary report?",
                        "How do I document site zones?",
                        "How do I compare crane alternatives?"
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "AI ASSIST",
                    Purpose = "Persistent right-side intelligence layer for crane-planning guidance, deterministic result analysis, local Ollama explanation, software navigation, and context export. AI Assist is not a top-level ribbon tab; advanced tools live in its Studio section.",
                    PrimaryView = "CraneOptimizer.AI/Companion/Views/AiCompanionPanel.xaml and Views/AiAdvisorWindow.xaml",
                    PrimaryViewModel = "AiCompanionViewModel and AiAdvisorViewModel",
                    Actions = new[]
                    {
                        Action("AI Assist / Mission Control", "AiCompanionPanel", "Use the docked AI Assist panel as a slim, evidence-first sidecar with a rotated vertical section rail for Chat, Mission, BIM, GA, Report, and AI Studio. The minimal surface keeps verified project state, one next-step recommendation, the active section, a private-context composer, and governed action status clearly separated. Chat receives the majority of the available height, the rail supports keyboard and mouse-wheel switching, and AI Assist shares the same 330 to 380 px right dock as Crane Properties so tab changes do not resize the 3D viewport.", "Always available from the main project window; opened by explicit user action. The state header, concise recommendation, composer, context inspector, and footer remain fixed while the active section scrolls independently.", "The companion explains, copies evidence, and proposes safe text/navigation actions but never changes crane data automatically."),
                        Action("AI Studio Showcase", "AiShowcaseService / AiCompanionPanel", "Visible AI surface showing the agent stack, live capabilities, demo prompt chips, explainable decision trace, and a copyable presentation brief.", "Available in AI Assist → Studio when live workflow context is available.", "Showcase text is generated from deterministic context and remains advisory; it does not mutate project or optimizer state."),
                        Action("Open Full Studio", "OpenFullStudioCommand", "Open the complete AI Optimization Studio window.", "Available in AI Assist → Studio.", "AI output is advisory only."),
                        Action("Analyze Current Result", "AnalyzeHereCommand / AnalyzeCurrentResultCommand", "Run deterministic advisor rules against the current optimization result.", "Available in AI Assist → Studio and works even without Ollama.", "Deterministic analysis explains app metrics but does not certify crane plans."),
                        Action("Ask Local Ollama", "PrepareLocalOllamaQuestionCommand / AskOllamaCommand", "Open the private AI Assist chat composer for the selected local Ollama model, or use the full Studio window for model controls.", "Available in AI Assist → Studio; requires an Ollama server and installed model for generative responses.", "Ollama receives structured context and must not override calculations."),
                        Action("Check Ollama", "CheckOllamaCommand", "Detect the local Ollama server and installed models.", "Available in the full AI Optimization Studio window.", "Does not install anything."),
                        Action("Pull Model", "PullModelCommand", "Ask Ollama to download the model named in the Model field.", "Explicit user action in the full AI Optimization Studio window; may take time and disk space.", "Do not pull very large models unless the machine has enough resources."),
                        Action("Copy Context", "CopyContextCommand", "Copy structured AI context JSON.", "Available after analysis.", "Context may include project names/paths; share carefully."),
                        Action("Copy Recovery Plan", "CopyRecoveryPlanCommand", "Copy the AI Mission Control recovery checklist with owner, evidence, verification, and safety boundary.", "Available from the relevant AI Assist section.", "Advisory text only; it does not change optimizer gates."),
                        Action("Copy What-if Advisor", "CopyWhatIfAdvisorCommand", "Copy the AI-ranked next investigation lever: data, zones, crane model, load chart, diagnostics, or reporting.", "Available from the relevant AI Assist section.", "Ranks investigation options but does not perform engineering changes."),
                        Action("Copy Report Text", "CopyReportNarrativeCommand", "Copy AI-assisted report language from deterministic project context.", "Available from the relevant AI Assist section.", "Reviewer approval is required before issuing any report language."),
                        Action("Copy QA Evidence Pack", "CopyQaEvidencePackCommand", "Copy readiness checklist, blocker summary, recovery plan, and what-if advisor together.", "Available from the relevant AI Assist section.", "Evidence pack can include project-identifying context; share carefully.")
                    },
                    TypicalQuestions = new[]
                    {
                        "What can this software do?",
                        "Which tab contains load chart import?",
                        "Guide me from IFC import to optimization report.",
                        "Explain current optimization result.",
                        "Show me the AI Studio capability stack and demo script."
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "SETTINGS",
                    Purpose = "User settings and preferences. This tab is currently hidden by AppFeatureFlags.ShowSettingsTab unless enabled.",
                    PrimaryView = "Features/F9_Settings/Views/UserSettingsView.xaml",
                    PrimaryViewModel = "Settings view models",
                    Actions = new[]
                    {
                        Action("User Settings", "Feature flag controlled", "Configure user/application settings when enabled.", "Hidden by default in the current product configuration.", "Do not instruct users to click this tab unless it is visible.")
                    },
                    TypicalQuestions = new[]
                    {
                        "Why can I not see Settings?",
                        "Where are user settings configured?"
                    }
                },
                new AiFeatureTabDto
                {
                    TabName = "COORDINATE REFERENCE",
                    Purpose = "Coordinate reference tools. This tab is currently hidden by AppFeatureFlags.ShowCoordinateReferenceUi unless enabled.",
                    PrimaryView = "Features/F18_CoordinateReference/Views/CoordinateReferenceView.xaml",
                    PrimaryViewModel = "CoordinateReferenceViewModel",
                    Actions = new[]
                    {
                        Action("Coordinate Reference", "Feature flag controlled", "Inspect or configure coordinate reference behavior when enabled.", "Hidden by default in the current product configuration.", "Do not instruct users to click this tab unless it is visible.")
                    },
                    TypicalQuestions = new[]
                    {
                        "How does coordinate reference affect alignment?",
                        "Why is the coordinate reference tab hidden?"
                    }
                }
            },
            Workflows = new[]
            {
                new AiWorkflowDto
                {
                    Name = "Normal optimization workflow",
                    Goal = "Move from raw IFC/site data to a reviewed crane placement result.",
                    RelatedTabs = new[] { "HOME", "IFC MODEL", "DXF", "ZONES", "CRANES", "ANALYZE", "AI ASSIST", "REPORTS" },
                    Steps = new[]
                    {
                        "HOME → New or Open project.",
                        "IFC MODEL → Load IFC, then verify element count, geometry, COG markers, and weights.",
                        "DXF → Upload and Align DXF if a CAD/site overlay is available.",
                        "ZONES → Draw allowed and forbidden zones or import zones from DXF layers.",
                        "CRANES → Add/select mobile crane, import/check load chart, and review crane catalog data.",
                        "ANALYZE → Run Optimizer.",
                        "ANALYZE or REPORTS → Open GA Diagnostics and View Results.",
                        "AI ASSIST → STUDIO → Analyze Current Result, then optionally use Ask Local Ollama to explain recommended next actions.",
                        "REPORTS → Generate lifting summary, elements list, project overview, site zones, or crane comparison reports."
                    }
                },
                new AiWorkflowDto
                {
                    Name = "Low coverage troubleshooting workflow",
                    Goal = "Diagnose why many elements are not liftable.",
                    RelatedTabs = new[] { "ANALYZE", "AI ASSIST", "ZONES", "CRANES", "IFC MODEL", "REPORTS" },
                    Steps = new[]
                    {
                        "AI ASSIST → STUDIO → Analyze Current Result to identify dominant failure categories.",
                        "ANALYZE → Open GA Diagnostics to check convergence and candidate behavior.",
                        "If failures are OutOfRadius, inspect ZONES and CRANES setup radius/allowed positions.",
                        "If failures are Overweight, inspect IFC element weights, rigging additional weight, derating, safety factor, and crane capacity.",
                        "If failures are NotCharted, inspect load chart import, boom/radius rows, operating condition, and number of lines.",
                        "If failures are HeightFail, inspect boom length, elevation, wire length, and geometry assumptions.",
                        "Re-run optimizer after changing inputs, then generate reports only from the updated result."
                    }
                },
                new AiWorkflowDto
                {
                    Name = "Report generation workflow",
                    Goal = "Create defensible project documentation after reviewing optimization results.",
                    RelatedTabs = new[] { "ANALYZE", "AI ASSIST", "REPORTS" },
                    Steps = new[]
                    {
                        "ANALYZE → View Results and GA Diagnostics.",
                        "AI ASSIST → STUDIO or CHAT → Generate or request a natural-language explanation using the current structured context.",
                        "REPORTS → Generate Lifting Summary Report for optimization outcome.",
                        "REPORTS → Generate All Elements List, Project Overview, and Site Zones Report as needed.",
                        "Attach or archive diagnostics CSVs when available."
                    }
                }
            },
            SafetyGuardrails = new[]
            {
                "AI Assist may explain, summarize, and suggest next software actions, but it must not certify crane lift plans.",
                "The deterministic optimizer, load chart lookup, zone constraints, and domain services remain the source of calculation truth.",
                "Final crane plans require qualified engineering review against manufacturer data, site rules, project requirements, and applicable safety standards.",
                "Do not invent hidden buttons, unimplemented features, standards, measurements, or project facts not supplied by the software context.",
                "Clearly distinguish visible features from requested actions that are not currently available in the supplied context."
            }
        };

        public AiSoftwareContextDto GetSoftwareContext() => Context;

        private static AiFeatureActionDto Action(
            string label,
            string commandOrHandler,
            string purpose,
            string availability,
            string safetyNote)
        {
            return new AiFeatureActionDto
            {
                Label = label,
                CommandOrHandler = commandOrHandler,
                Purpose = purpose,
                Availability = availability,
                SafetyNote = safetyNote
            };
        }
    }
}
