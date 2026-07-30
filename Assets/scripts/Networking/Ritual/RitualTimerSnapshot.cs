using System;

namespace Incantation.Networking.Ritual
{
    /// <summary>
    /// Immutable value-only state for the server-authoritative ritual timer.
    /// Network timestamps use FishNet's shared time domain.
    /// </summary>
    public readonly struct RitualTimerSnapshot
    {
        public RitualTimerSnapshot(
            uint timerSequence,
            RitualSequenceId ritualSequenceId,
            RitualTurnSequenceId turnSequenceId,
            bool isRunning,
            bool isExpired,
            double duration,
            double startNetworkTime,
            double deadlineNetworkTime,
            double remainingTime)
        {
            TimerSequence = timerSequence;
            RitualSequenceId = ritualSequenceId;
            TurnSequenceId = turnSequenceId;
            IsRunning = isRunning;
            IsExpired = isExpired;
            Duration = SanitizeNonNegative(duration);
            StartNetworkTime = SanitizeNonNegative(startNetworkTime);
            DeadlineNetworkTime = SanitizeNonNegative(deadlineNetworkTime);
            RemainingTime = SanitizeNonNegative(remainingTime);
        }

        public uint TimerSequence { get; }
        public RitualSequenceId RitualSequenceId { get; }
        public RitualTurnSequenceId TurnSequenceId { get; }
        public bool IsRunning { get; }
        public bool IsExpired { get; }
        public double Duration { get; }
        public double StartNetworkTime { get; }
        public double DeadlineNetworkTime { get; }
        public double RemainingTime { get; }

        private static double SanitizeNonNegative(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0d
                ? 0d
                : value;
        }
    }
}
