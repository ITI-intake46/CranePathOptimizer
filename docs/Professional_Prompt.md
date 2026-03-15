Professional Prompt for UI
////////////////////////////////////////////////////////////////////
You are working on the UI layer of a BIM-based crane optimization system.

The system is built with:

C#
.NET 8
WPF
HelixToolkit
Clean Architecture

Project layers:

Domain
Application
Infrastructure
UI

The UI responsibilities are:

1. Load IFC models
2. Display elements in 3D
3. Allow user to draw crane placement zones
4. Allow user to assign elements to cranes
5. Send GASettings to the optimization engine
6. Visualize crane positions returned by GA

The UI must NOT implement any optimization logic.

All optimization logic exists inside:

Application/Optimization/GeneticAlgorithms

The UI only calls:

IGeneticAlgorithmEngine.Run()

Inputs sent to the GA:

- crane placement zones
- precast elements
- user assignment
- crane specification
- GASettings

Outputs from GA:

CraneSolution

which contains optimized crane positions.
////////////////////////////////////////////////////////////////////





Professional Prompt for GA
////////////////////////////////////////////////////////////////////
You are implementing the Genetic Algorithm engine of a crane placement optimization system.

The project uses Clean Architecture.

You are working inside:

Application/Optimization/GeneticAlgorithms

Important classes:

GAEngine
GAInitializer
FitnessEvaluator
ElementAssignmentService
TournamentSelection
CrossoverOperator
MutationOperator
ReplacementStrategy

Your task is to implement GA operators only.

The pipeline is:

Initialize population
Evaluate assignment
Evaluate fitness
Selection
Crossover
Mutation
Replacement

Do not modify:

GAEngine
GASettings
Domain entities
UI layer

Each operator must be implemented in its own file.
////////////////////////////////////////////////////////////////////