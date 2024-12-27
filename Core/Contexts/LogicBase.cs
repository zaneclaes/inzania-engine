namespace IZ.Core.Contexts;

public abstract class LogicBase : ContextualObject, IAmInternal {
  protected override bool AllowRootContext => true;

  protected LogicBase(ITuneContext? context = null) : base(context) { }

  protected override string ContextualObjectGroup => "Logic";
}
