using System;
using System.ComponentModel.Composition;

namespace BitMaker.Miner.Cpu
{

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CpuSolverAttribute : ExportAttribute, ICpuSolverMetadata
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CpuSolverAttribute()
            : base(typeof(CpuSolver))
        {

        }

    }

    public interface ICpuSolverMetadata
    {


    }

}
