using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conventor2;

using Auction = IReadOnlyList<Bid>;
public record struct Bid(int Level = 0, ContractStrain Strain = ContractStrain.None) : IComparable<Bid>, IEquatable<Bid> {
    public static readonly Bid Pass = new(0, ContractStrain.None);
    public static readonly Bid Double = new(-1, ContractStrain.None);
    public static readonly Bid Redouble = new(-2, ContractStrain.None);

    public static List<Bid> AllBids { get; } = _GenerateAllBids().ToList();

    private static IEnumerable<Bid> _GenerateAllBids() {
        yield return Pass;
        yield return Double;
        yield return Redouble;
        
        for (var level = 1; level <= 7; level++) {
            for (var contractStrain = (int)ContractStrain.Clubs; contractStrain <= (int)ContractStrain.NoTrump; contractStrain++) {
                yield return new Bid { Level = level, Strain = (ContractStrain)contractStrain };
            }
        }
    }

    public bool IsPass => Level == 0 && Strain == ContractStrain.None;
    public bool IsDouble => Level == 1 && Strain == ContractStrain.None;
    public bool IsRedouble => Level == 2 && Strain == ContractStrain.None;

    public Bid NextStep() {
        return this with
        {
            Level = Strain == ContractStrain.NoTrump ? Level + 1 : Level,
            Strain = Strain.Next()
        };
    }

    public int CompareTo(Bid other) {
        var comparisonResult = this.Level.CompareTo(other.Level);
        if (comparisonResult != 0) {
            return comparisonResult;
        }

        comparisonResult = Strain.CompareTo(other.Strain);
        if (comparisonResult != 0) {
            return comparisonResult;
        }

        return 0;
    }

    public static bool operator <(Bid left, Bid right) {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Bid left, Bid right) {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Bid left, Bid right) {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Bid left, Bid right) {
        return left.CompareTo(right) >= 0;
    }

    public bool IsValid {
        get {
            if (Level <= 0) {
                if (Level < Redouble.Level) {
                    return false;
                }

                return Strain == ContractStrain.None;
            }

            return Level <= 7 && Strain != ContractStrain.None;
        }
    }

    public override string ToString() {
        return (Level, Strain) switch {
            (-1, _) => "X",
            (-2, _) => "XX",
            (_, ContractStrain.Clubs) => $"{Level}C",
            (_, ContractStrain.Diamonds) => $"{Level}D",
            (_, ContractStrain.Hearts) => $"{Level}H",
            (_, ContractStrain.Spades) => $"{Level}S",
            (_, ContractStrain.NoTrump) => $"{Level}NT",
            _ => "P",
        };
    }
}

public enum ContractStrain : int
{
    None,
    Clubs,
    Diamonds,
    Hearts,
    Spades,
    NoTrump
}

public static class BiddingExtensions
{
    public static ContractStrain Next(this ContractStrain suit)
    {
        return suit switch
        {
            ContractStrain.Clubs => ContractStrain.Diamonds,
            ContractStrain.Diamonds => ContractStrain.Hearts,
            ContractStrain.Hearts => ContractStrain.Spades,
            ContractStrain.Spades => ContractStrain.NoTrump,
            ContractStrain.NoTrump => ContractStrain.Clubs,
            _ => ContractStrain.None,
        };
    }

    public static bool IsLegalBiddingSequence(Auction bids) {
        return true;
    }

    public static bool IsNextBidLegal(this Auction bids, Bid nextBid) {
        if (nextBid.Level > 7 || nextBid.Level < 0) {
            return false;
        }

        if (nextBid.IsPass) {
            // Check if this will be the 4th pass in the auction.
            // If it is, disallow it (for our purposes don't consider
            // the case of 4 opening passes, this is for conventions,
            // not play)
            if (bids.Count >= 3 && bids.TakeLast(3).All(b => b.IsPass)) {
                return false;
            }
        }

        if (nextBid.IsDouble) {
            // Dobule is legal as long as we've:
            //  Had an opening bid
            //  The last bid in the auction was not a double
            //  The last bid in the auction was not a redouble
            //  The second to last bid a pass (cannot double partners bid)
            if (bids.Count < 4 && !bids.Any(b => b.IsPass)) {
                return false;
            }

            var lastContract = bids.Last(b => !b.IsPass);
            if (lastContract.IsDouble || lastContract.IsRedouble) {
                return false;
            }

            if (bids.Count >= 2) {
                var finalBids = bids.TakeLast(2).ToArray();
                if (!finalBids[0].IsPass && finalBids[1].IsPass) {
                    return false;
                }
            }

            return true;
        }

        if (nextBid.IsRedouble) {
            // Redouble is only legal if:
            //  The last bid in the auction was a double
            //  The last bid in the auction was not by our partner
            if (bids.Count < 2) {
                return false;
            }

            var lastContract = bids.Last(b => b.IsPass);
            if (!lastContract.IsDouble) {
                return false;
            }

            var finalBids = bids.TakeLast(2).ToArray();
            if (!finalBids[0].IsPass && finalBids[1].IsPass) {
                return false;
            }
        }

        // Make sure the bid is strictly greater than the last one bid.
        var lastContractBid = bids.LastOrDefault(b => !b.IsPass);
        return nextBid > lastContractBid;
    }

    public static IEnumerable<Bid> AllChildBids(this Auction auction) {
        var lastBid = auction.LastOrDefault(b => !b.IsPass);
        if (lastBid.IsPass) {
            foreach (var openingBid in Bid.AllBids) {
                if (auction.Count == 3 && openingBid.IsPass) {
                    continue;
                }

                if (openingBid.IsDouble || openingBid.IsRedouble) {
                    continue;
                }

                yield return openingBid;
            }

            yield break;
        }

        if (auction.Count < 3) {
            yield return Bid.Pass;
        } else {
            if (auction[auction.Count - 1].IsPass && auction[auction.Count - 2].IsPass && auction[auction.Count - 3].IsPass) {
                // Do nothing
            } else { yield return Bid.Pass; }
        }

        if (auction.Count > 0) {
            if (auction[auction.Count - 1] != Bid.Pass) { 
                yield return Bid.Double;
            }

            if (auction[auction.Count - 1] == Bid.Double) {
                yield return Bid.Redouble;
            }
        }
        if (auction.Count > 1) {
            if (auction[auction.Count - 2] == Bid.Pass) {
                yield return Bid.Double;
            }
        }
        if (auction.Count > 2) {
            if (auction[auction.Count - 2] == Bid.Pass && auction[auction.Count - 3] == Bid.Double) {
                yield return Bid.Redouble;
            }
        }

        var nextBid = lastBid.NextStep();
        while (nextBid.IsValid) {
            yield return nextBid;
            nextBid = lastBid.NextStep();
        }
    }

    public static string ToSequenceString(this Auction bids) {
        return "";
    }
}
