#region

using System;

#endregion

namespace IZ.Core.Data;

public interface ICreatedAt {
  DateTime CreatedAt { get; set; }
}

public interface IUpdatedAt {
  DateTime? UpdatedAt { get; set; }
}

public interface ITimeStampData : ICreatedAt, IUpdatedAt { }
