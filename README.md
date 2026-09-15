# Sandbox
Welcome to the SandBox. Ideas are allowed to come to life here without the burden of being perfect. 

It is meant to inspire others to try their own ideas, or to take this further and refine it.

## ConnectionExplorer
Bible cross-references, followed further down the chain than usual. Instead of
showing only the references on a verse, it walks the reference graph a few hops
out and scores what it finds. Verses that are connected indirectly, or that
reference *into* your verse, surface too.

> A hosted version lives at
[crossreferences.org](https://crossreferences.org/sandbox/connection-explorer?b=19&ch=23&v=1&v2=9&tr=BSB&n=30).

You can select more than one verse as a starting point.

<img width="786" height="635" alt="image" src="https://github.com/user-attachments/assets/4e2a6ce8-9711-4080-935e-45d493ad7ba1" />


A heatmap shows how results are distributed. It doubles as a filtering mechanism.
To see results only from the Psalms and Proverbs, click on those.

<img width="781" height="290" alt="image" src="https://github.com/user-attachments/assets/3ed151c4-77b5-42f5-b9f9-ea584d9aac2a" />

Results are aggregated in a way that automatically includes context.
The top-scoring verses are in black, while less relevant ones surrounding
them are greyed based on score.

<img width="786" height="812" alt="image" src="https://github.com/user-attachments/assets/ef88b653-8235-4755-bc08-a43d5a41e640" />

### Scoring

Each level contributes less to the final score than the one before it:

| | |
|---|---|
| **L1** | Direct references in this verse |
| **Incoming** | Verses which reference this verse |
| **L2** | References found for verses in L1 |
| **L3** | References found for verses in L2 |

The weights and indicators are still moving, so treat the numbers as indicative.
`CountInfo.cs` is where they live if you want to argue with them.

# How to Run
You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download). 
Clone this repository then:
1. run `dotnet run --project SandBox`
2. look for the line that says something like `Now listening on: http://localhost:5039`.
   This means you can go to `http://localhost:5039/` in your browser

Or if you are in Visual Studio, press `F5` and it will open a browser window
for you.

The data ships with the repo, so there's nothing to wire up.

# Technical details

.NET 10, Blazor Server. The choice was made based mainly on the fact that I am 
fluent in C# (not Blazor though), and I find it easiest to reason about a problem
when I do not have to stop to look up syntax. Django could have been chosen, as I
do have a soft spot for it, but my Python is not up to standard. Furthermore, the 
graph walk fans out to thousands of verses per query, so execution speed matters.

Everything is in-memory: the app loads the JSON snapshot in `json/` at startup
and keeps it in frozen dictionaries. No database, no setup.

Not hardened for public hosting. I do host it publicly, but the traffic is low
enough that it hasn't mattered. Its purpose is to be an experiment people can
play with, which carries a different burden than something built for consumers.

# Data

`json/` is an export from
[CrossReferences.org](https://github.com/CrossReferences-org/bible-cross-references),
built on the Treasury of Scripture Knowledge. Cross-reference data ©
CrossReferences.org, licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
Frozen snapshot. Corrections belong in the data repository, not here.

Bible texts: KJV, BSB and AOV, all public domain.
