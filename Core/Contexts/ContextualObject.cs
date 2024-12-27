#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using IZ.Core.Api.Types;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Exceptions;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Contexts;

public abstract class ContextualObject : IDisposable, IEventEnricher {
  private readonly ITuneContext? _baseContext;
  private ITuneContext? _context;
  private ITuneLogger? _logger;

  [ApiIgnore]
  public virtual string Uuid => _uuid ??= GetUuid();
  private string? _uuid;

  internal TuneTypeDescriptor ApiType => _apiType ??= TuneTypeDescriptor.FromType(GetType());
  private TuneTypeDescriptor? _apiType;

  private string GetUuid() => GetType().Name + "#" + UuidId;
  protected virtual string UuidId => _transientId ??= ModelId.GenerateId();
  private string? _transientId;

  public override string ToString() => Uuid;

  // Low-cardinality grouping of objects used for metric tags & event property groups
  protected abstract string ContextualObjectGroup { get; }

  [ApiIgnore] [JsonIgnore]
  public Dictionary<string, object> EventProperties => _eventProperties ??= BuildEventProperties();
  private Dictionary<string, object>? _eventProperties;
  private Dictionary<string, object> BuildEventProperties() => Context.EventProperties
    .Union(GetObservableProperties())
    .Union(new Dictionary<string, object> {
      [$"{ContextualObjectGroup}.{GetType().Name}.Id"] = UuidId
    }).ToDictionary(k => k.Key, k => k.Value);

  [ApiIgnore] [JsonIgnore]
  public Dictionary<string, object> EventTags => _eventTags ??= BuildEventTags();
  private Dictionary<string, object>? _eventTags;
  private Dictionary<string, object> BuildEventTags() => Context.EventTags
    .Union(GetObservableProperties(true))
    .Union(new Dictionary<string, object> {
      ["object_group"] = ContextualObjectGroup
    }).ToDictionary(k => k.Key, k => k.Value);

  protected virtual Dictionary<string, object> GetObservableProperties(bool isMetric = false) {
    Dictionary<string, object> ret = new Dictionary<string, object>();
    foreach (var prop in ApiType.ObjectDescriptor.AllProperties) {
      if (prop.Observable == null) continue;
      if (isMetric && prop.Observable.MetricName == null) continue;
      string key = isMetric ? prop.Observable.MetricName! : prop.FieldName;
      object? val = prop.GetValue(this);
      if (val != null) ret[key] = val;
    }
    return ret;
  }

  protected ContextualObject(ITuneContext? context = null) {
    _baseContext = context;
  }

  [JsonIgnore] [ApiIgnore]
  protected bool IsDisposed { get; private set; }

  public virtual void Dispose() {
    if (IsDisposed) return;
    IsDisposed = true;
  }

  [NotMapped] [ApiIgnore] [JsonIgnore]
  public ITuneLogger Log => _logger ??= Context.Log;

  [NotMapped] [ApiIgnore] [JsonIgnore]
  public ITuneContext Context {
    get => _context ??= SpawnInContext(_baseContext);
    set {
      if (_context != null) {
        if (value == _context) return;
        if (!(_context is ITuneRootContext)) throw new InternalTuneException(Context, $"Double-context on {GetType()}: {_context} -> {value}");
      }
      // if (value is ChildContext child && child.ScopeType == GetType())
      // else _context = value.Spawn(GetType());
      _context = value;
      // Log.Information("[CONTEXT] {type}: {trace}", GetType(), new TuneTrace());
    }
  }

  // Set the internal context if it is not set, thus preventing a root context error
  public void ProvideContext(ITuneContext context) {
    if (_context == null) _context = context;
  }

  // Use with care... bypasses checks to ensure context on this and all child objects
  public bool EnforceContext(ITuneContext context) {
    if (_context == context) return false;
    // try {
    //   HashSet<string> breadcrumbs = new HashSet<string> {Uuid};
    //   EnforceDataContext(context, breadcrumbs);
    // } catch (Exception e) {
    //   context.Log.Error(e, "Failed to enforce context on {type}", GetType());
    // }

    _context = context;
    return true;
  }

  // We enforce that DataObject children always get context assigned, in case they were loaded from the database
  private void EnforceDataContext(ITuneContext context, HashSet<string> breadcrumbs) {
    _context = context;
    context.Log.Information("[OBJ] {type} {uuid}", GetType(), Uuid);
    // context.Log.Verbose("[OBJ] {type} :: {oprops} {aprops}", GetType(), desc.ObjectProperties.Keys, desc.ScalarProperties.Keys);
    List<TunePropertyDescriptor>? props = ApiType.ObjectDescriptor.ObjectProperties.Values.ToList(); //.Where(p => p.ExecutionMethod != null);
    foreach (var prop in props) {
      object? children = prop.GetValue(this);
      if (children is IList list) {
        context.Log.Information("[LIST] {type}.{name} = {count}", GetType(), prop.FieldName, list.Count);
        for (int i = 0; i < list.Count; i++) {
          // context.Log.Verbose("[LIST] {type}.{name} = {ch}", GetType(), prop.FieldName, list[i]?.GetType());
          if (list[i] is DataObject c && breadcrumbs.Add(c.Uuid)) c.EnforceDataContext(context, breadcrumbs);
        }
      } else if (children is DataObject child) {
        if (breadcrumbs.Add(child.Uuid)) {
          child.EnforceDataContext(context, breadcrumbs);
        }
      } else if (children is ApiObject o) { } else if (children != null) {
        context.Log.Warning("[CHILDREN] {type}.{name} is a {child}", GetType(), prop.FieldName, children.GetType());
      }
    }
  }

  protected virtual bool AllowRootContext => false;

  protected virtual ITuneContext SpawnInContext(ITuneContext? context) {
    if (context != null) return context;
    var ret = IZEnv.SpawnRootContext();
    if (!AllowRootContext) ret.Log.Warning("[OBJ] {type} spawned root context: {obj}", GetType(), this);
    return ret;
  }
}
