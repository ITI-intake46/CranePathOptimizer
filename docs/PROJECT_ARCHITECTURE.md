# Crane Optimizer – Architecture Overview

The system follows **Clean Architecture**.

Layers:

UI
Application
Domain
Infrastructure

---

## Domain Layer

Contains pure business entities.

Entities:

- PrecastElement
- Crane
- CraneSpecification
- TargetPoint
- CranePlacementZone
- Point3D

Domain has **no dependency on other layers**.

---

## Application Layer

Contains optimization logic.

Folders:

Optimization/
    GeneticAlgorithms/

Key Components:

Models
Interfaces
Services
Engine
Calculations
Settings

Main classes:

GAEngine
GAInitializer
FitnessEvaluator
ElementAssignmentService
TournamentSelection
CrossoverOperator
MutationOperator
ReplacementStrategy
LiftCalculation

---

## Infrastructure Layer

Responsible for:

- Database access (SQLite)
- IFC parsing using xBIM
- Repository implementations

Key classes:

- IfcReader
- CraneRepository
- CraneDbContext

---

## UI Layer

WPF Desktop Application.

Responsibilities:

- Load IFC files
- Display elements in HelixToolkit
- Draw crane zones
- Send GASettings
- Send element assignment
- Visualize results