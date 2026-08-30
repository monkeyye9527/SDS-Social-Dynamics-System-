using System;

namespace ONIModPack.Core.Services
{
    public struct InfluenceState
    {
        public float Value;
        public float Strength;
        public int SourceId;
        public int Cell;

        public InfluenceState(float value, float strength, int sourceId, int cell)
        {
            Value = value;
            Strength = strength;
            SourceId = sourceId;
            Cell = cell;
        }
    }

    public interface IInfluenceSource
    {
        int SourceId { get; }
        
        InfluenceState Evaluate(int cell);
        
        void Update(float deltaTime);
    }

    public interface IInfluenceSolver
    {
        void Update(Span<InfluenceState> states);
        
        float GetInfluence(int cell);
        
        void Reset();
    }
}
