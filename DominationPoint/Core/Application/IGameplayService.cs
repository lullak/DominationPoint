namespace DominationPoint.Core.Application
{
    public interface IGameplayService
    {
        Task<(bool Success, string Message)> CaptureControlPointAsync(int cpId, string userId, string numpadCode);
    }
}
