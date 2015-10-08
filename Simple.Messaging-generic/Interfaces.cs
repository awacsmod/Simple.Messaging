using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simple.Messaging
{
    internal interface IFlowElement { }

    internal interface ITaskElement 
    {
        /// <summary>
        /// Gets the task of an asynchronously executing element
        /// </summary>
        Task MyTask { get; }
    }

    internal interface IRun
    {
        /// <summary>
        /// Executes the function.
        /// </summary>
        void Run();
    }

    internal interface IRun<T>
    {
        /// <summary>
        /// Executes the function.
        /// </summary>
        /// <param name="param">The input parameter that has been created elsewhere.</param>
        void Run(T param);
    }

    internal interface IDone
    {
        /// <summary>
        /// Occurs after the Run method has finished successfully.
        /// </summary>
        event Action Done;
    }

    internal interface IDone<T>
    {
        /// <summary>
        /// Occurs after the Run method has finished successfully.
        /// </summary>
        event Action<T> Done;
    }

    internal interface IFail
    {
        /// <summary>
        /// Occurs if an error has been thrown during the execution of the Run method.
        /// </summary>
        event Action<Exception> Fail;
    }

    internal interface IAlways
    {
        /// <summary>
        /// Occurs after the method has finished, no matter if successfully or with an error.
        /// </summary>
        event Action Always;
    }

    internal interface IFunction : IRun, IDone, IFail, IAlways, IFlowElement, ITaskElement 
    { }

    internal interface IFunction<T1, T2> : IRun<T1>, IDone<T2>, IFail, IAlways, IFlowElement, ITaskElement
    { }

    internal interface IInitFunction<T> : IRun, IDone<T>, IFail, IAlways, IFlowElement, ITaskElement
    { }

    internal interface INoReturnFunction<T> : IRun<T>, IDone, IFail, IAlways, IFlowElement, ITaskElement
    { }
}
