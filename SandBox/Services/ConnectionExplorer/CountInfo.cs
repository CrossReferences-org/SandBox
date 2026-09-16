using System.Runtime.CompilerServices;

namespace SandBox.Services.ConnectionExplorer;

public record struct CountInfo(double L1, double L2, double L3, double L1_Incoming, double L1_Incoming_L1_Overlap)
{
    [Flags]
    public enum ScoreElements
    {
        None = 0,
        L1 = 1,
        L2 = 2,
        L3 = 4,
        Incoming = 1024,
        OutGoing = L1 | L2 | L3,
        All = Incoming | OutGoing,
    }

    public const double L1_Avg = 19.136528173751483; // Computed from data on 20260430
    public const double L2_Avg = 497.30944677579527; // Computed from data on 20260430
    public const double L3_Avg = 14076.700035237211; // Computed from data on 20260430
    public const double L1_to_L2 = L2_Avg / L1_Avg;
    public const double L1_to_L3 = L3_Avg / L1_Avg;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double WeightedScore(ScoreElements scoreElements) 
        => (Score_L1 * (scoreElements.HasFlag(ScoreElements.L1) ? 1 : 0))
         + (Score_L2 * (scoreElements.HasFlag(ScoreElements.L2) ? 1 : 0))
         + (Score_L3 * (scoreElements.HasFlag(ScoreElements.L3) ? 1 : 0))
         + (Score_L1Incoming * (scoreElements.HasFlag(ScoreElements.Incoming) ? 1 : 0))
         + (Score_L1_Incoming_L1_Overlap * (scoreElements.HasFlag(ScoreElements.Incoming) ? 1 : 0));
    public readonly double Score_L1Incoming => L1_Incoming * (L1 == 0 ? 0.7 : 0.2);
    public readonly double Score_L1_Incoming_L1_Overlap => Score_L1Incoming * Math.Min(0.5, L1_Incoming_L1_Overlap / 2);// / L1_to_L2;

    // 0.75 =>
    // 1 : 1    **********| 
    // 2 : 1.68 ********** *******...|
    // 3 : 2.28 ********** ********** ***.......|
    // 4 : 2.83 ********** ********** ********.. ..........|
    // 5 : 3.34 ********** ********** ********** ***....... ..........| 
    // 6 : 3.83 ********** ********** ********** ********.. .......... ..........|
    // 7 : 4.30 ********** ********** ********** ********** ***....... .......... ..........|
    // 8 : 4.76 ********** ********** ********** ********** ********.. .......... .......... ..........|
    public readonly double Score_L1 => Math.Pow(L1, 0.75); // drop off on L1 duplication. Inconclusive, and not sure how to reason about this yet.
    public readonly double Score_L2 => L2 / (L1_to_L2 * 1.1892);
    public readonly double Score_L3 => L3 / (L1_to_L3 * 1.4142);

    public readonly double Count() => L1_Incoming + L1 + L2 + L3;

    public readonly int GetPrimaryConnectionKind() => this switch
    {
        { L1: > 0 } => 1,
        { L1: 0, L1_Incoming: > 0 } => 2,
        { L1: 0, L1_Incoming: 0, L2: > 0 } => 3,
        _ => 0
    };

    public readonly int GetShallowestLevel() => this switch
    {
        { L1: > 0 } => 1,
        { L1: 0, L1_Incoming: > 0 } => 2,
        { L1: 0, L1_Incoming: 0, L2: > 0 } => 3,
        _ => 0
    };
}
