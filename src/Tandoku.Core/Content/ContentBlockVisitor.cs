namespace Tandoku.Content;

public abstract class ContentBlockVisitor<T>
{
    public abstract T Visit(TextBlock block);
    public abstract T Visit(CompositeBlock block);
}
