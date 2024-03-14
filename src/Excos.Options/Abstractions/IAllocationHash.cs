// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

namespace Excos.Options.Abstractions
{
    /// <summary>
    /// Algorithm for computing a hash of the identifier for use in allocation calculations.
    /// </summary>
    public interface IAllocationHash
    {
        /// <summary>
        /// Computes a hash of the identifier for use in allocation calculations.
        /// </summary>
        /// <returns>A value between 0 and 1.</returns>
        double GetAllocationSpot(string salt, string identifier);
    }
}
