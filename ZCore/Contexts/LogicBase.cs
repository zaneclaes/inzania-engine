namespace IZ.Core.Contexts;

public abstract class LogicBase : ContextualObject, IAmInternal {

  protected LogicBase(IZContext? context = null) : base(context) { }
  protected override bool AllowRootContext => true;

  protected override string ContextualObjectGroup => "Logic";
}
