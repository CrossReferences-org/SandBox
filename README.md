# Sandbox
Welcome to the SandBox. Ideas are allowed to come to life here without the burden of being perfect.

## ConnectionExplorer
Bible cross-references, followed further down the chain than usual. Instead of
showing only the references on a verse, it walks the reference graph a few hops
out and scores what it finds — so verses that are connected indirectly, or that
reference *into* your verse, surface too.

See it live [here](https://crossreferences.org/sandbox/connection-explorer?b=19&ch=23&v=1&ch2=&v2=9&tr=BSB&n=30)

<img width="705" height="737" alt="image" src="https://github.com/user-attachments/assets/6a939383-7c5d-4461-950e-87b4e8a52369" />
<img width="697" height="325" alt="image" src="https://github.com/user-attachments/assets/cf4407ae-a990-4327-aed0-9c6cab79bad6" />
<img width="701" height="818" alt="image" src="https://github.com/user-attachments/assets/588ef221-19c8-4307-8d75-4374dc5e7ec2" />

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

### Running it
```
dotnet run
```
Blazor Server, .NET 10. 

Everything is in-memory: the app loads the JSON snapshot in `json/` at startup
and keeps it in frozen dictionaries. No database, no setup.

Not hardened for public hosting; it's built to be run locally and poked at.


## Data

`json/` is an export from
[CrossReferences.org](https://github.com/CrossReferences-org/bible-cross-references),
built on the Treasury of Scripture Knowledge. Cross-reference data ©
CrossReferences.org, licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
Frozen snapshot. Corrections belong in the data repository, not here.

Bible texts: KJV, BSB and AOV, all public domain.
