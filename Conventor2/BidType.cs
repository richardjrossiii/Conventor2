using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conventor2;

using Auction = IReadOnlyList<BidType>;
public record struct BidType(int Level = 0, ContractStrain Strain = ContractStrain.None, string? AlertTag = null) : IComparable<BidType>, IEquatable<BidType> {
    public static readonly BidType Pass = new(0, ContractStrain.None);
    public static readonly BidType Double = new(-1, ContractStrain.None);
    public static readonly BidType Redouble = new(-2, ContractStrain.None);

    public static BidType Unexpanded(string bidToExpand) {
        return new BidType(-3, ContractStrain.None) {
            UnexpandedBid = bidToExpand,
        };
    }

    public string? UnexpandedBid { get; set; }

    public static List<BidType> AllBids { get; } = _GenerateAllBids().ToList();

    private static IEnumerable<BidType> _GenerateAllBids() {
        yield return Pass;
        yield return Double;
        yield return Redouble;
        
        for (var level = 1; level <= 7; level++) {
            for (var contractStrain = (int)ContractStrain.Clubs; contractStrain <= (int)ContractStrain.NoTrump; contractStrain++) {
                yield return new BidType { Level = level, Strain = (ContractStrain)contractStrain };
            }
        }
    }

    public bool IsPass => Level == 0 && Strain == ContractStrain.None;
    public bool IsDouble => Level == -1 && Strain == ContractStrain.None;
    public bool IsRedouble => Level == -2 && Strain == ContractStrain.None;

    public BidType NextStep() {
        return this with
        {
            Level = Strain == ContractStrain.NoTrump ? Level + 1 : Level,
            Strain = Strain.Next()
        };
    }

    public int CompareTo(BidType other) {
        if (Strain == ContractStrain.None) {
            if (other.Strain == ContractStrain.None) {
                return -(Level.CompareTo(other.Level));
            } else {
                return -1;
            }
        }

        var comparisonResult = Level.CompareTo(other.Level);
        if (comparisonResult != 0) {
            return comparisonResult;
        }

        comparisonResult = Strain.CompareTo(other.Strain);
        if (comparisonResult != 0) {
            return comparisonResult;
        }

        return 0;
    }

    public static bool operator <(BidType left, BidType right) {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(BidType left, BidType right) {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(BidType left, BidType right) {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(BidType left, BidType right) {
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
        if (UnexpandedBid != null) {
            return UnexpandedBid;
        }

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
    public static string ToShortString(this ContractStrain strain) {
        return strain switch {
            ContractStrain.Clubs => "C",
            ContractStrain.Diamonds => "D",
            ContractStrain.Hearts => "H",
            ContractStrain.Spades => "S",
            ContractStrain.NoTrump => "NT",
            _ => "",
        };
    }

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

    public static bool IsLegalBiddingSequence(this Auction bids) {
        BidType highestBid = BidType.Pass;
        BidType lastNonPass = BidType.Pass;
        int passes = 0;

        foreach (var nextBid in bids) {
            if (nextBid.IsPass) {
                // Pass is always legal unless it would end the auction
                if (passes == 3) {
                    return false;
                }

                passes++;
                continue;
            }

            if (nextBid.IsDouble) {
                if (passes == 1 || passes == 3) {
                    // you cannot double your partner
                    return false;
                }

                if (lastNonPass.IsDouble) {
                    // Cannot double an already doubled contract
                    return false;
                }
            } else if (nextBid.IsRedouble) {
                if (passes == 1 || passes == 3) {
                    // you cannot redouble your partner
                    return false;
                }

                if (lastNonPass.IsRedouble) {
                    // Cannot double an already redoubled contract
                    return false;
                }
            } else {
                // New suit builds are always legal as long as they're ascending in value
                if (highestBid >= nextBid) {
                    return false;
                }

                if (nextBid.Level == 2 && nextBid.Strain == ContractStrain.Spades) {
                    Debug.Print("test");
                }

                highestBid = nextBid;
            }

            passes = 0;
            lastNonPass = nextBid;
        }

        return true;
    }

    public static bool IsNextBidLegal(this Auction bids, BidType nextBid) {
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

    public static IEnumerable<BidType> AllChildBids(this Auction auction) {
        var lastBid = auction.LastOrDefault(b => !b.IsPass);
        if (lastBid.IsPass) {
            foreach (var openingBid in BidType.AllBids) {
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
            yield return BidType.Pass;
        } else {
            if (auction[auction.Count - 1].IsPass && auction[auction.Count - 2].IsPass && auction[auction.Count - 3].IsPass) {
                // Do nothing
            } else { yield return BidType.Pass; }
        }

        if (auction.Count > 0) {
            if (auction[auction.Count - 1] != BidType.Pass) { 
                yield return BidType.Double;
            }

            if (auction[auction.Count - 1] == BidType.Double) {
                yield return BidType.Redouble;
            }
        }
        if (auction.Count > 1) {
            if (auction[auction.Count - 2] == BidType.Pass) {
                yield return BidType.Double;
            }
        }
        if (auction.Count > 2) {
            if (auction[auction.Count - 2] == BidType.Pass && auction[auction.Count - 3] == BidType.Double) {
                yield return BidType.Redouble;
            }
        }

        var nextBid = lastBid.NextStep();
        while (nextBid.IsValid) {
            yield return nextBid;
            nextBid = lastBid.NextStep();
        }
    }

    public static string ToSequenceString(this Auction bids) {
        return string.Join("-", bids.Select(b => b.ToString()));
    }

    public static List<BidType> ToBiddingSequence(this BidType? bid) {
        if (bid == null) {
            return [];
        }

        return [bid ?? BidType.Pass];
    }
}
