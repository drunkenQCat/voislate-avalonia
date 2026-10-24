using VoiSlate.Infrastructure;

namespace VoiSlate.Desktop;

public interface IWorkspaceTransaction
{
    void Begin();
    void Commit();
    void Rollback();
}
public sealed class LiteDbWorkspaceTransaction(LiteDbStore store) : IWorkspaceTransaction
{
    public void Begin() { if (!store.Database.BeginTrans()) throw new InvalidOperationException("工作台事务已经开始"); }
    public void Commit() => store.Database.Commit();
    public void Rollback() => store.Database.Rollback();
}
