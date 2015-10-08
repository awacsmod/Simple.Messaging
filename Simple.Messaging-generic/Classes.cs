using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Simple.Messaging
{
    /// <summary>
    /// Abstract base class for both the generic and non-generic Simple.Messaging.FlowController classes.
    /// </summary>
    public abstract class FlowControllerBase: IFail, IAlways
    {
        internal List<Task> tasks = new List<Task>();
        internal List<IFlowElement> elements = new List<IFlowElement>();
        internal CancellationTokenSource source  = new CancellationTokenSource();
        internal void callFailAndAlways(Exception ex) 
        {
            callFail(ex);
            callAlways();
        }

        internal void callAlways() 
        { if (this.Always != null) { Always(); } }

        internal void callFail(Exception ex)
        { 
            if (this.Fail != null) { Fail(ex); }
            this.source.Cancel();
        }

        internal IFlowElement register(IFlowElement element)
        {
            this.elements.Add(element);
            this.tasks.Add(((ITaskElement)element).MyTask);

            if (this.elements.Count > 1)
            {
                IFlowElement previous = this.elements[this.elements.Count - 2];
                if (!(previous is IDone))
                {
                    this.elements.Remove(this.elements.Last());
                    this.tasks.Remove(this.tasks.Last());
                    throw new FlowRegisterException(previous.GetIDoneType());
                }
                else
                {
                    ((IDone)previous).Done += ((IRun)this.elements.Last()).Run;
                }
            }
            ((IFail)this.elements.Last()).Fail += this.callFailAndAlways;
            return this.elements.Last();
        }

        internal IFlowElement register<T>(IFlowElement element)
        {
            this.elements.Add(element);
            this.tasks.Add(((ITaskElement)element).MyTask);

            if (this.elements.Count > 1)
            {
                IFlowElement previous = this.elements[this.elements.Count - 2];
                if (!(previous is IDone<T>))
                {
                    this.elements.Remove(this.elements.Last());
                    this.tasks.Remove(this.tasks.Last());
                    Type iDoneType = previous.GetIDoneType();
                    if (iDoneType == typeof(void)) { throw new FlowRegisterException(); }
                    else { throw new FlowRegisterException(iDoneType); }
                }
                else
                {
                    ((IDone<T>)previous).Done += ((IRun<T>)this.elements.Last()).Run;
                }
            }
            ((IFail)this.elements.Last()).Fail += this.callFailAndAlways;
            return this.elements.Last();
        }

        internal void registerAdditionalEvents(ref IFlowElement element, Action onDone, Action<Exception> onFail, Action onAlways)
        {
            if (onDone != null) { ((IDone)element).Done += onDone; }
            if (onFail != null) { ((IFail)element).Fail += onFail; }
            if (onAlways != null) { ((IAlways)element).Always += onAlways; }
        }

        internal void registerAdditionalEvents<TResult>(ref IFlowElement element, Action<TResult> onDone, Action<Exception> onFail, Action onAlways)
        {
            if (onDone != null) { ((IDone<TResult>)element).Done += onDone; }
            if (onFail != null) { ((IFail)element).Fail += onFail; }
            if (onAlways != null) { ((IAlways)element).Always += onAlways; }
        }

        /// <summary>
        /// Occurs if an exception was thrown during the execution of a method.
        /// </summary>
        public event Action<Exception> Fail;

        /// <summary>
        /// Occurs after the last method has completed or after an exception has been thrown.
        /// </summary>
        public event Action Always;

        /// <summary>
        /// Executes the methods that have previously been registered.
        /// </summary>
        /// <exception cref="Simple.Messaging.EmptyFlowException">Occurs if no method has been registered.</exception>
        /// <exception cref="Simple.Messaging.FlowStartException">Occurs if the first method expects an input parameter.</exception>
        public void Run()
        {
            RunAsync();
            WaitAll();
        }

        /// <summary>
        /// Asynchronously executes the methods that have previously been registered.
        /// </summary>
        /// <exception cref="Simple.Messaging.EmptyFlowException">Occurs if no method has been registered.</exception>
        /// <exception cref="Simple.Messaging.FlowStartException">Occurs if the first method expects an input parameter.</exception>
        public void RunAsync()
        {
            if (this.elements.Count == 0)
            {
                throw new EmptyFlowException();
            }
            else if (!(this.elements[0] is IRun))
            {
                throw new FlowStartException(this.elements[0].GetIRunType());
            }
            else
            {
                ((IAlways)this.elements.Last()).Always += callAlways;
                ((IRun)this.elements[0]).Run(); 
            }
        }

        /// <summary>
        /// When executing the methods asynchronously, waits for all methods to be finished.
        /// </summary>
        public void WaitAll()
        {
            Task.WaitAll(this.tasks.ToArray(), this.source.Token);
        }
    }

    /// <summary>
    /// Registers and asynchronously or synchronously executes loosely coupled methods.
    /// </summary>
    public class FlowController : FlowControllerBase, IFail, IAlways
    {
        private object currentMessage;

        /// <summary>
        /// The current status of the last manipulated value.
        /// </summary>
        public object CurrentMessage 
        {
            get { return this.currentMessage; }
            internal set { this.currentMessage = value; }
        }

        /// <summary>
        /// Creates a new instance of the Simple.Messaging.FlowController class.
        /// </summary>
        public FlowController()
        {
            this.currentMessage = null;
        }

        /// <summary>
        /// Registers a method in the flow.
        /// </summary>
        /// <param name="method">The method to register.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        /// <exception cref="Simple.Messaging.FlowRegisterException">Occurs if the previous method returns a value.</exception>
        public void Register(Action method, Action onDone = null, Action<Exception> onFail = null, Action onAlways = null)
        {
            this.register(new Function(method, this.source.Token));
            IFlowElement f = this.register(new Function(() => { this.CurrentMessage = null; }, this.source.Token));
            this.registerAdditionalEvents(ref f, onDone, onFail, onAlways);
        }

        /// <summary>
        /// Registers a method in the flow.
        /// </summary>
        /// <typeparam name="TResult">The type of the return value of the method.</typeparam>
        /// <param name="method">The method to register.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        /// <exception cref="Simple.Messaging.FlowRegisterException">Occurs if the previous method returns a value.</exception>
        public void RegisterNoParam<TResult>(Func<TResult> method, Action<TResult> onDone = null, Action<Exception> onFail = null, Action onAlways = null)
        {
            this.register(new InitFunction<TResult>(method, this.source.Token));
            IFlowElement f = this.register<TResult>(new Function<TResult, TResult>((value) => { this.CurrentMessage = value; return value; }, this.source.Token));
            this.registerAdditionalEvents<TResult>(ref f, onDone, onFail, onAlways);
        }

        /// <summary>
        /// Registers a method in the flow.
        /// </summary>
        /// <typeparam name="T">The type of the input parameter of the method.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method.</typeparam>
        /// <param name="method">The method to register.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        /// <exception cref="Simple.Messaging.FlowRegisterException">Occurs if the input type provided does not match the return type of the previous method.</exception>
        public void Register<T, TResult>(Func<T, TResult> method, Action<TResult> onDone = null, Action<Exception> onFail = null, Action onAlways = null)
        {
            this.register<T>(new Function<T, TResult>(method, this.source.Token));
            IFlowElement f = this.register<TResult>(new Function<TResult, TResult>((value) => { this.CurrentMessage = value; return value; }, this.source.Token));
            this.registerAdditionalEvents<TResult>(ref f, onDone, onFail, onAlways);
        }

        /// <summary>
        /// Registers a method in the flow.
        /// </summary>
        /// <typeparam name="T">The type of the input parameter of the method.</typeparam>
        /// <param name="method">The method to register.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        /// <exception cref="Simple.Messaging.FlowRegisterException">Occurs if the type provided does not match the return type of the previous method.</exception>
        public void Register<T>(Action<T> method, Action onDone = null, Action<Exception> onFail = null, Action onAlways = null)
        {
            this.register<T>(new NoReturnFunction<T>(method, this.source.Token));
            IFlowElement f = this.register(new Function(() => { this.CurrentMessage = null; }, this.source.Token));
            this.registerAdditionalEvents(ref f, onDone, onFail, onAlways);
        }

        /// <summary>
        /// Registers a method in the flow.
        /// </summary>
        /// <typeparam name="T">The type of the input parameter of the method.</typeparam>
        /// <param name="method">The method to register.</param>
        /// <param name="constant">The input parameter value of the method.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        /// <exception cref="Simple.Messaging.FlowRegisterException">Occurs if the type provided does not match the return type of the previous method.</exception>
        public void RegisterWithConstant<T>(Action<T> method, T constant, Action onDone = null, Action<Exception> onFail = null, Action onAlways = null) 
        {
            this.register(new InitFunction<T>(() => { return constant; }, this.source.Token));
            Register<T>(method, onDone, onFail, onAlways);
        }

        /// <summary>
        /// Registers a method in the flow.
        /// </summary>
        /// <typeparam name="T">The type of the input parameter of the method.</typeparam>
        /// <typeparam name="TResult">The type of the return value of the method.</typeparam>
        /// <param name="method">The method to register.</param>
        /// <param name="constant">The input parameter value of the method.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        /// <exception cref="Simple.Messaging.FlowRegisterException">Occurs if the type provided does not match the return type of the previous method.</exception>
        public void RegisterWithConstant<T, TResult>(Func<T, TResult> method, T constant, Action<TResult> onDone = null, Action<Exception> onFail = null, Action onAlways = null)
        {
            this.register(new InitFunction<T>(() => { return constant; }, this.source.Token));
            Register<T, TResult>(method, onDone, onFail, onAlways);
        }

        /// <summary>
        /// Executes the methods that have previously been registered.
        /// </summary>
        /// <typeparam name="T">The type of the input parameter of the first registered method.</typeparam>
        /// <param name="param">The input parameter of the first registered method.</param>
        /// <exception cref="Simple.Messaging.EmptyFlowException">Occurs if no method has been registered.</exception>
        /// <exception cref="Simple.Messaging.FlowStartException">Occurs if the type provided does not match the type of the input parameter of the first method.</exception>
        public void Run<T>(T param)
        {
            RunAsync<T>(param);
            WaitAll();
        }

        /// <summary>
        /// Asynchronously executes the methods that have previously been registered.
        /// </summary>
        /// <typeparam name="T">The type of the input parameter of the first registered method.</typeparam>
        /// <param name="param">The input parameter of the first registered method.</param>
        /// <exception cref="Simple.Messaging.EmptyFlowException">Occurs if no method has been registered.</exception>
        /// <exception cref="Simple.Messaging.FlowStartException">Occurs if the type provided does not match the type of the input parameter of the first method.</exception>
        public void RunAsync<T>(T param)
        {
            if (this.elements.Count == 0)
            {
                throw new EmptyFlowException();
            }
            else if (!(this.elements[0] is IRun<T>))
            {
                Type iRunType = this.elements[0].GetIRunType();
                if (iRunType == typeof(void)) { throw new FlowStartException(); }
                else { throw new FlowStartException(iRunType); }
            }
            else
            {
                ((IAlways)this.elements.Last()).Always += base.callAlways;
                this.currentMessage = param;
                ((IRun<T>)this.elements[0]).Run(param); 
            }
        }
    }

    /// <summary>
    /// Registers and asynchronously or synchronously executes loosely coupled methods.
    /// </summary>
    /// <typeparam name="T">The type of the object ("message") the registered methods manipulate.</typeparam>
    public class FlowController<T> : FlowControllerBase
    {
        private T currentMessage;

        /// <summary>
        /// The current message status.
        /// </summary>
        public T CurrentMessage 
        {
            get { return this.currentMessage; }
            internal set { this.currentMessage = value; }
        }

        /// <summary>
        /// Creates a new instance of the generic Simple.Messaging.FlowController class.
        /// </summary>
        public FlowController()
        {
            this.currentMessage = default(T);
        }
        /// <summary>
        /// Creates a new instance of the generic Simple.Messaging.FlowController class.
        /// </summary>
        /// <param name="message">The initial message status.</param>
        public FlowController(T message)
        {
            this.currentMessage = message;
        }

        /// <summary>
        /// Delegate type for methods that can be registered in this Simple.Messaging.FlowController.
        /// </summary>
        /// <param name="message">The ingoing status of the message.</param>
        public delegate void MessageMethodDelegate(ref T message);

        /// <summary>
        /// Delegate type for router methods that can be registered in this Simple.Messaging.FlowController.
        /// </summary>
        /// <param name="controller">The Simple.Messaging.FlowController to be extended by the method.</param>
        public delegate void MessageRouterDelegate(ref FlowController<T> controller);

        /// <summary>
        /// Registers a method in the flow.
        /// </summary>
        /// <param name="method">The method to register.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        public void Register(MessageMethodDelegate method, Action onDone = null, Action<Exception> onFail = null, Action onAlways = null) 
        {
            IFlowElement f = this.register(new NoReturnDelegateFunction<T>(method, this, this.source.Token));
            this.registerAdditionalEvents(ref f, onDone, onFail, onAlways);
        }

        /// <summary>
        /// Registers a method in the flow.
        /// </summary>
        /// <param name="method">The method to register.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        public void Register(Func<T, T> method, Action onDone = null, Action<Exception> onFail = null, Action onAlways = null)
        {
            this.register(new InitFunction<T>(() => { return this.currentMessage; }, this.source.Token));
            this.register<T>(new Function<T, T>(method, this.source.Token));
            IFlowElement f = this.register<T>(new NoReturnFunction<T>((param) => { this.currentMessage = param; }, this.source.Token));
            this.registerAdditionalEvents(ref f, onDone, onFail, onAlways);
        }

        /// <summary>
        /// Registers a routing method in the flow.
        /// </summary>
        /// <param name="method">The method to register.</param>
        /// <param name="onDone">Optional method that is executed after the method has finished successfully (e.g. logging or branching).</param>
        /// <param name="onFail">Optional method that is executed after the method has finished with an error (e.g. logging or branching).</param>
        /// <param name="onAlways">Optional method that is executed after the method has finished (e.g. logging or branching).</param>
        public void RegisterRouter(Action<FlowController<T>> method, Action onDone = null, Action<Exception> onFail = null, Action onAlways = null) 
        {
            IFlowElement f = this.register(new RouterFunction<T>(method, this, this.source.Token));
            this.registerAdditionalEvents(ref f, onDone, onFail, onAlways);
        }
    }

    /// <summary>
    /// Represents errors that occur during flow registration or execution.
    /// </summary>
    public class FlowException : Exception
    {
        internal string message;

        internal FlowException()
        { this.message = string.Empty; }

        internal FlowException(string message)
        {
            this.message = message;
        }

        /// <summary>
        /// Gets a message that describes the current Exception.
        /// </summary>
        public override string Message
        {
            get { return this.message; }
        }
    }

    /// <summary>
    /// Represents errors that occur when a flow ist started.
    /// </summary>
    public class FlowStartException : FlowException 
    {
        internal FlowStartException(Type type)
        { this.message = string.Format("The first method requires an input parameter of type '{0}'.", type.FullName); }

        internal FlowStartException()
        { this.message = "The first method does not accept input parameters."; }
    }

    /// <summary>
    /// Represents errors that occur when an empty flow is started.
    /// </summary>
    public class EmptyFlowException : FlowStartException
    {
        internal EmptyFlowException() 
        { this.message = "Cannot run a flow that has no registered methods."; }
    }

    /// <summary>
    /// Represents errors that occur during flow execution.
    /// </summary>
    public class FlowExecutionException : FlowException
    {
        private Exception innerException;
        private string methodName;

        internal FlowExecutionException(string methodName, Exception innerException)
        {
            this.methodName = methodName;
            this.message = string.Format("An error occured during the execution of method '{0}'.", this.methodName);
            this.innerException = innerException;
        }

        /// <summary>
        /// Gets the name of the method that threw the exception.
        /// </summary>
        public string MethodName
        {
            get { return this.methodName; }
        }

        /// <summary>
        /// Gets the System.Exception instance that caused the current exception.
        /// </summary>
        public new Exception InnerException
        {
            get { return this.innerException; }
        }

        /// <summary>
        /// Creates and returns astring representation of the current exception.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        { return base.ToString() + Environment.NewLine + this.innerException.ToString(); }
    }

    /// <summary>
    /// Represents errors that occur during flow method registration.
    /// </summary>
    public class FlowRegisterException : FlowException
    {
        internal FlowRegisterException()
        { this.message = "The previous method does not return a value."; }

        internal FlowRegisterException(Type type)
        { this.message = string.Format("The previous method returns a value of type '{0}'.", type.FullName); }
    }

    class Function : IFunction
    {
        Action method;
        Task myTask;

        public Task MyTask
        {
            get { return this.myTask; }
        }

        public Function(Action method, CancellationToken token)
        { 
            this.method = method;
            this.myTask = new Task(() => {
                try
                {
                    method.Invoke();
                    if (Done != null) { Done(); }
                }
                catch (Exception ex)
                {
                    if (Fail != null) { Fail(new FlowExecutionException(method.Method.Name, ex)); }
                }
                finally
                {
                    if (Always != null) { Always(); }
                }
            }, token);
        }

        public void Run()
        {
            myTask.Start();
        }

        public event Action Done;

        public event Action<Exception> Fail;

        public event Action Always;
    }

    class Function<T, TResult> : IFunction<T, TResult>
    {
        Func<T, TResult> method;
        Task myTask;
        T param;

        public Task MyTask
        {
            get { return this.myTask; }
        }

        public Function(Func<T, TResult> method, CancellationToken token)
        { 
            this.method = method;
            this.myTask = new Task(() =>
            {
                try
                {
                    TResult result = method.Invoke(this.param);
                    if (Done != null) { Done(result); }
                }
                catch (Exception ex)
                {
                    if (Fail != null) { Fail(new FlowExecutionException(method.Method.Name, ex)); }
                }
                finally
                {
                    if (Always != null) { Always(); }
                }
            }, token);
        }

        public void Run(T param)
        {
            this.param = param;
            this.myTask.Start();
        }

        public event Action<TResult> Done;

        public event Action<Exception> Fail;

        public event Action Always;
    }

    class InitFunction<TResult> : IInitFunction<TResult>
    {
        Func<TResult> method;
        Task myTask;

        public Task MyTask
        {
            get { return this.myTask; }
        }

        public InitFunction(Func<TResult> method, CancellationToken token)
        { 
            this.method = method;
            this.myTask = new Task(() => {
                try
                {
                    TResult result = method.Invoke();
                    if (Done != null) { Done(result); }
                }
                catch (Exception ex)
                {
                    if (Fail != null) { Fail(new FlowExecutionException(method.Method.Name, ex)); }
                }
                finally
                {
                    if (Always != null) { Always(); }
                }
            }, token);
        }

        public void Run()
        {
            this.myTask.Start();
        }

        public event Action<TResult> Done;

        public event Action<Exception> Fail;

        public event Action Always;
    }

    class NoReturnFunction<T> : INoReturnFunction<T>
    {
        Action<T> method;
        Task myTask;
        T param;

        public Task MyTask
        {
            get { return this.myTask; }
        }

        public NoReturnFunction(Action<T> method, CancellationToken token)
        { 
            this.method = method;
            this.myTask = new Task(() =>
            {
                try
                {
                    method.Invoke(this.param);
                    if (Done != null) { Done(); }
                }
                catch (Exception ex)
                {
                    if (Fail != null) { Fail(new FlowExecutionException(method.Method.Name, ex)); }
                }
                finally
                {
                    if (Always != null) { Always(); }
                }
            }, token);
        }

        public void Run(T param)
        {
            this.param = param;
            this.myTask.Start();
        }

        public event Action Done;

        public event Action<Exception> Fail;

        public event Action Always;
    }

    class NoReturnDelegateFunction<T> : IFunction
    {
        FlowController<T>.MessageMethodDelegate method;
        FlowController<T> ctrl;
        Task myTask;

        public Task MyTask
        {
            get { return this.myTask; }
        }

        public NoReturnDelegateFunction(FlowController<T>.MessageMethodDelegate method, FlowController<T> ctrl, CancellationToken token)
        {
            this.ctrl = ctrl;
            this.method = method;
            this.myTask = new Task(() =>
            {
                try
                {
                    T param = this.ctrl.CurrentMessage;
                    method.Invoke(ref param);
                    this.ctrl.CurrentMessage = param;

                    if (Done != null) { Done(); }
                }
                catch (Exception ex)
                {
                    if (Fail != null) { Fail(new FlowExecutionException(method.Method.Name, ex)); }
                }
                finally
                {
                    if (Always != null) { Always(); }
                }
            }, token);
        }

        public void Run()
        {
            this.myTask.Start();
        }

        public event Action Done;

        public event Action<Exception> Fail;

        public event Action Always;
    }

    class RouterFunction<T> : IFunction 
    {
        Action<FlowController<T>> method;
        Task myTask;
        FlowController<T> ctrl;

        public Task MyTask
        {
            get { return this.myTask; }
        }

        public RouterFunction(Action<FlowController<T>> method, FlowController<T> ctrl, CancellationToken token)
        { 
            this.method = method;
            this.ctrl = ctrl;
            this.myTask = new Task(() =>
            {
                try
                {
                    ((IAlways)ctrl.elements.Last()).Always -= ctrl.callAlways;
                    this.method.Invoke(this.ctrl);
                    ((IAlways)ctrl.elements.Last()).Always += ctrl.callAlways;
                    if (Done != null) { Done(); }
                }
                catch (Exception ex)
                {
                    if (Fail != null) { Fail(new FlowExecutionException(this.method.Method.Name, ex)); }
                }
                finally
                {
                    if (Always != null) { Always(); }
                }
            }, token);
        }

        public void Run()
        {
            this.myTask.Start();
        }

        public event Action Done;

        public event Action<Exception> Fail;

        public event Action Always;
    }

    static class Extensions
    {
        internal static Type GetIRunType (this IFlowElement element) 
        {
            return getGenericInterfaceType(element, "IRun");
        }

        internal static Type GetIDoneType(this IFlowElement element)
        {
            return getGenericInterfaceType(element, "IDone");
        }

        private static Type getGenericInterfaceType(IFlowElement element, string name)
        {
            Type result = getInterfaceType(element, name);
            if (result == default(Type)) { return typeof(void); }
            else if (!result.IsGenericType) { return typeof(void); }
            else { return result.GetGenericArguments()[0]; }
        }

        private static Type getInterfaceType(IFlowElement element, string name)
        {
            Type[] interfaces = element.GetType().GetInterfaces();
            return interfaces.FirstOrDefault((t) => { return t.Name.ToLower().StartsWith(name.ToLower()); });
        }
    }

    /// <summary>
    /// Represents a collection of keys and dynamic values.
    /// </summary>
    public class DynamicDictionary : Dictionary<string, dynamic>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public new dynamic this[string key]
        {
            get { return base[key]; }
            set 
            {
                if (base.Keys.Contains(key))
                { base[key] = value; }
                else { this.Add(key, value); }
            }
        }
    }
}
