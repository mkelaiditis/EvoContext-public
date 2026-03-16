namespace EvoContext.Core.AdaptiveMemory;

public interface IRun2QueryBuilder
{
    RetrievalQuerySet Build(string baseQuery, FeedbackOutput feedback);
}
