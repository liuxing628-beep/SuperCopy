namespace SuperCopy
{
    // Require a short intentional hover, like grouping icons on a phone. Passing
    // across a tile while reordering must not unexpectedly merge its contents.
    internal sealed class MergeHover
    {
        internal const int HoldMilliseconds = 450;
        internal int Candidate = -1;
        internal int Target = -1;
        private long enteredAt;
        internal void Update(int candidate, long milliseconds)
        {
            if (candidate < 0) { Reset(); return; }
            if (candidate != Candidate) {
                Candidate = candidate; Target = -1; enteredAt = milliseconds;
            } else if (milliseconds - enteredAt >= HoldMilliseconds) Target = candidate;
        }
        internal void Reset() { Candidate = Target = -1; enteredAt = 0; }
    }
}
