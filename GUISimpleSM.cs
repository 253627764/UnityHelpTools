using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class GUISimpleSM<TState, TTrigger>
{
    internal abstract class TriggerBehaviour
    {
        private TTrigger mTrigger;
        private Func<bool> mGuard;
        protected TriggerBehaviour(TTrigger trigger, Func<bool> guard)
        {
            mTrigger = trigger;
            mGuard = guard;
        }
        public TTrigger Trigger
        {
            get
            {
                return mTrigger;
            }
        }
        public bool IsGuardConditionOk
        {
            get
            {
                return mGuard();
            }
        }
        public abstract bool ResultsInTransitionFrom(TState source, object[] args, out TState destination);
    }

    internal class TransitioningTriggerBehaviour : TriggerBehaviour
    {
        private TState mDestination;
        public TransitioningTriggerBehaviour(TTrigger trigger, TState destination, Func<bool> guard)
            : base(trigger, guard)
        {
            mDestination = destination;
        }

        public override bool ResultsInTransitionFrom(TState source, object[] args, out TState destination)
        {
            destination = mDestination;
            return true;
        }
    }
    internal class IgnoredTriggerBehaviour : TriggerBehaviour
    {
        public IgnoredTriggerBehaviour(TTrigger trigger, Func<bool> guard)
            : base(trigger, guard)
        {
        }
        public override bool ResultsInTransitionFrom(TState source, object[] args, out TState destination)
        {
            destination = default(TState);
            return false;
        }
    }
    internal class DynamicTriggerBehaviour : TriggerBehaviour
    {
        private Func<object[], TState> mDestination;

        public DynamicTriggerBehaviour(TTrigger trigger, Func<object[], TState> destination, Func<bool> guard)
            : base(trigger, guard)
        {
            mDestination = destination;
        }

        public override bool ResultsInTransitionFrom(TState source, object[] args, out TState destination)
        {
            destination = mDestination(args);
            return true;
        }
    }
    public class Transition
    {
        private TState mSource;
        private TState mDestination;
        private TTrigger mTrigger;

        public Transition(TState source, TState destination, TTrigger trigger)
        {
            mSource = source;
            mDestination = destination;
            mTrigger = trigger;
        }

        public TState Source
        {
            get
            {
                return mSource;
            }
        }

        public TState Destination
        {
            get
            {
                return mDestination;
            }
        }

        public TTrigger Trigger
        {
            get
            {
                return mTrigger;
            }
        }

        public bool IsReentry
        {
            get
            {
                return Source.Equals(Destination);
            }
        }
    }
    internal class StateRepresentation
    {
        private TState mState;
        private IDictionary<TTrigger, ICollection<TriggerBehaviour>> mTriggerBehaviours = new Dictionary<TTrigger, ICollection<TriggerBehaviour>>();

        private ICollection<Action<Transition, object[]>> mEntryActions = new List<Action<Transition, object[]>>();
        private ICollection<Action<Transition>> mExitActions = new List<Action<Transition>>();

        private StateRepresentation mSuperState; // null
        private ICollection<StateRepresentation> mSubStates = new List<StateRepresentation>();

        public StateRepresentation(TState state)
        {
            mState = state;
        }

        public bool CanHandle(TTrigger trigger)
        {
            TriggerBehaviour unused;
            return TryFindHandler(trigger, out unused);
        }

        public bool TryFindHandler(TTrigger trigger, out TriggerBehaviour handler)
        {
            return (TryFindLocalHandler(trigger, out handler) ||
                (mSuperState != null && mSuperState.TryFindHandler(trigger, out handler)));
        }

        private bool TryFindLocalHandler(TTrigger trigger, out TriggerBehaviour handler)
        {
            ICollection<TriggerBehaviour> possible;
            if (!mTriggerBehaviours.TryGetValue(trigger, out possible))
            {
                handler = null;
                return false;
            }

            List<TriggerBehaviour> guardOkList = new List<TriggerBehaviour>();
            foreach (TriggerBehaviour tgb in possible)
            {
                if (tgb.IsGuardConditionOk)
                {
                    guardOkList.Add(tgb);
                }
            }
            if (guardOkList != null && guardOkList.Count > 1)
            {
                Debug.Log("cant a trigger have muti behaviours.");
                handler = null;
                return false;
            }
            handler = guardOkList.Count == 1 ? guardOkList[0] : null;
            return handler != null;
        }

        public void AddEntryAction(TTrigger trigger, Action<Transition, object[]> action)
        {
            if (action != null)
            {
                mEntryActions.Add((t, args) =>
                {
                    if (t.Trigger.Equals(trigger))
                        action(t, args);
                });
            }
        }

        public void AddEntryAction(Action<Transition, object[]> action)
        {
            if (action != null)
            {
                mEntryActions.Add(action);
            }
        }

        public void AddExitAction(Action<Transition> action)
        {
            if (action != null)
            {
                mExitActions.Add(action);
            }
        }

        public bool IncludeState(TState state)
        {
            if (mState.Equals(state))
            {
                return true;
            }
            foreach (StateRepresentation sr in mSubStates)
            {
                if (sr.IncludeState(state))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsIncludedIn(TState state)
        {
            return
                mState.Equals(state) ||
                (mSuperState != null && mSuperState.IsIncludedIn(state));
        }

        public IEnumerable<TTrigger> PermittedTriggers
        {
            get
            {
                List<TTrigger> possible = new List<TTrigger>();
                foreach (KeyValuePair<TTrigger, ICollection<TriggerBehaviour>> tbs in mTriggerBehaviours)
                {
                    foreach (TriggerBehaviour tb in tbs.Value)
                    {
                        if (tb.IsGuardConditionOk)
                        {
                            possible.Add(tbs.Key);
                        }
                    }
                }

                if (mSuperState != null)
                {
                    foreach (TTrigger pt in mSuperState.PermittedTriggers)
                    {
                        if (!possible.Contains(pt))
                        {
                            possible.Add(pt);
                        }
                    }
                }
                return possible;
            }
        }

        public void AddSubstate(StateRepresentation substate)
        {
            if (substate != null)
            {
                mSubStates.Add(substate);
            }
        }

        public TState UnderlyingState
        {
            get
            {
                return mState;
            }
        }

        public StateRepresentation Superstate
        {
            get
            {
                return mSuperState;
            }
            set
            {
                mSuperState = value;
            }
        }

        public void AddTriggerBehaviour(TriggerBehaviour triggerBehaviour)
        {
            ICollection<TriggerBehaviour> allowed;
            if (!mTriggerBehaviours.TryGetValue(triggerBehaviour.Trigger, out allowed))
            {
                allowed = new List<TriggerBehaviour>();
                mTriggerBehaviours.Add(triggerBehaviour.Trigger, allowed);
            }
            allowed.Add(triggerBehaviour);
        }

        private void ExecuteEntryActions(Transition transition, object[] entryArgs)
        {
            if (transition != null && entryArgs != null)
            {
                foreach (Action<Transition, object[]> action in mEntryActions)
                    action(transition, entryArgs);
            }
        }

        public void Enter(Transition transition, params object[] entryArgs)
        {
            if (transition != null)
            {
                if (transition.IsReentry)
                {
                    ExecuteEntryActions(transition, entryArgs);
                }
                else if (!IncludeState(transition.Source))
                {
                    if (mSuperState != null)
                        mSuperState.Enter(transition, entryArgs);
                    ExecuteEntryActions(transition, entryArgs);
                }
            }
        }

        private void ExecuteExitActions(Transition transition)
        {
            if (transition != null)
            {
                foreach (Action<Transition> action in mExitActions)
                    action(transition);
            }
        }

        public void Exit(Transition transition)
        {
            if (transition != null)
            {
                if (transition.IsReentry)
                {
                    ExecuteExitActions(transition);
                }
                else if (!IncludeState(transition.Destination))
                {
                    ExecuteExitActions(transition);
                    if (mSuperState != null)
                        mSuperState.Exit(transition);
                }
            }
        }
    }
    static class ParameterConversion
    {
        public static object Unpack(object[] args, Type argType, int index)
        {
            if (args != null && index < args.Length)
            {
                object arg = args[index];
                if (arg != null)
                {
                    if (argType.IsAssignableFrom(arg.GetType()))
                    {
                        return arg;
                    }
                    else
                    {
                        Debug.Log("index={0}, arg={1}, argType={2}", index, arg.GetType(), argType);
                    }
                }
            }
            return null;
        }

        public static TArg Unpack<TArg>(object[] args, int index)
        {
            return (TArg)Unpack(args, typeof(TArg), index);
        }

        public static void Validate(object[] args, Type[] expected)
        {
            if (args.Length <= expected.Length)
            {
                for (int i = 0; i < args.Length; ++i)
                    Unpack(args, expected[i], i);
            }
        }
    }
    public abstract class TriggerWithParameters
    {
        private TTrigger mUnderlyingTrigger;
        private Type[] mArgumentTypes;

        public TriggerWithParameters(TTrigger underlyingTrigger, params Type[] argumentTypes)
        {
            if (argumentTypes != null)
            {
                mUnderlyingTrigger = underlyingTrigger;
                mArgumentTypes = argumentTypes;
            }
        }

        public TTrigger Trigger
        {
            get
            {
                return mUnderlyingTrigger;
            }
        }

        public void ValidateParameters(object[] args)
        {
            if (args != null)
            {
                ParameterConversion.Validate(args, mArgumentTypes);
            }
        }
    }
    public class TriggerWithParameters<TArg0> : TriggerWithParameters
    {
        public TriggerWithParameters(TTrigger underlyingTrigger)
            : base(underlyingTrigger, typeof(TArg0))
        {
        }
    }

    public class TriggerWithParameters<TArg0, TArg1> : TriggerWithParameters
    {
        public TriggerWithParameters(TTrigger underlyingTrigger)
            : base(underlyingTrigger, typeof(TArg0), typeof(TArg1))
        {
        }
    }

    public class TriggerWithParameters<TArg0, TArg1, TArg2> : TriggerWithParameters
    {
        public TriggerWithParameters(TTrigger underlyingTrigger)
            : base(underlyingTrigger, typeof(TArg0), typeof(TArg1), typeof(TArg2))
        {
        }
    }
    public class StateConfiguration
    {
        private StateRepresentation mRepresentation;
        private Func<TState, StateRepresentation> mLookup;
        static private Func<bool> NoGuard = () => true;

        internal StateConfiguration(StateRepresentation representation, Func<TState, StateRepresentation> lookup)
        {
            mRepresentation = representation;
            mLookup = lookup;
        }

        public StateConfiguration Permit(TTrigger trigger, TState destinationState)
        {
            EnforceNotIdentityTransition(destinationState);
            return InternalPermit(trigger, destinationState);
        }

        public StateConfiguration PermitIf(TTrigger trigger, TState destinationState, Func<bool> guard)
        {
            EnforceNotIdentityTransition(destinationState);
            return InternalPermitIf(trigger, destinationState, guard);
        }

        public StateConfiguration PermitReentry(TTrigger trigger)
        {
            return InternalPermit(trigger, mRepresentation.UnderlyingState);
        }

        public StateConfiguration PermitReentryIf(TTrigger trigger, Func<bool> guard)
        {
            return InternalPermitIf(trigger, mRepresentation.UnderlyingState, guard);
        }

        private StateConfiguration InternalPermit(TTrigger trigger, TState destinationState)
        {
            return InternalPermitIf(trigger, destinationState, NoGuard);
        }

        private StateConfiguration InternalPermitIf(TTrigger trigger, TState destinationState, Func<bool> guard)
        {
            mRepresentation.AddTriggerBehaviour(new TransitioningTriggerBehaviour(trigger, destinationState, guard));
            return this;
        }

        public StateConfiguration Ignore(TTrigger trigger)
        {
            return IgnoreIf(trigger, NoGuard);
        }

        public StateConfiguration IgnoreIf(TTrigger trigger, Func<bool> guard)
        {
            mRepresentation.AddTriggerBehaviour(new IgnoredTriggerBehaviour(trigger, guard));
            return this;
        }

        public StateConfiguration OnEntry(Action entryAction)
        {
            return OnEntry(t => entryAction());
        }

        public StateConfiguration OnEntry(Action<Transition> entryAction)
        {
            mRepresentation.AddEntryAction((t, args) => entryAction(t));
            return this;
        }

        public StateConfiguration OnEntry<TArg0>(Action<TArg0> entryAction)
        {
            return OnEntry<TArg0>((a0, t) => entryAction(a0));
        }

        public StateConfiguration OnEntry<TArg0>(Action<TArg0, Transition> entryAction)
        {
            mRepresentation.AddEntryAction((t, args) => entryAction(ParameterConversion.Unpack<TArg0>(args, 0), t));
            return this;
        }

        public StateConfiguration OnEntry<TArg0, TArg1>(Action<TArg0, TArg1> entryAction)
        {
            return OnEntry<TArg0, TArg1>((a0, a1, t) => entryAction(a0, a1));
        }

        public StateConfiguration OnEntry<TArg0, TArg1>(Action<TArg0, TArg1, Transition> entryAction)
        {
            mRepresentation.AddEntryAction((t, args) => entryAction(ParameterConversion.Unpack<TArg0>(args, 0), ParameterConversion.Unpack<TArg1>(args, 1), t));
            return this;
        }

        public StateConfiguration OnEntry<TArg0, TArg1, TArg2>(Action<TArg0, TArg1, TArg2> entryAction)
        {
            return OnEntry<TArg0, TArg1, TArg2>((a0, a1, a2, t) => entryAction(a0, a1, a2));
        }

        public StateConfiguration OnEntry<TArg0, TArg1, TArg2>(Action<TArg0, TArg1, TArg2, Transition> entryAction)
        {
            mRepresentation.AddEntryAction((t, args) => entryAction(ParameterConversion.Unpack<TArg0>(args, 0),
                ParameterConversion.Unpack<TArg1>(args, 1), ParameterConversion.Unpack<TArg2>(args, 2), t));
            return this;
        }

        public StateConfiguration OnEntryFrom(TTrigger trigger, Action entryAction)
        {
            return OnEntryFrom(trigger, t => entryAction());
        }

        public StateConfiguration OnEntryFrom(TTrigger trigger, Action<Transition> entryAction)
        {
            mRepresentation.AddEntryAction(trigger, (t, args) => entryAction(t));
            return this;
        }

        public StateConfiguration OnEntryFrom<TArg0>(TriggerWithParameters<TArg0> trigger, Action<TArg0> entryAction)
        {
            return OnEntryFrom<TArg0>(trigger, (a0, t) => entryAction(a0));
        }

        public StateConfiguration OnEntryFrom<TArg0>(TriggerWithParameters<TArg0> trigger, Action<TArg0, Transition> entryAction)
        {
            mRepresentation.AddEntryAction(trigger.Trigger, (t, args) => entryAction(
                ParameterConversion.Unpack<TArg0>(args, 0), t));
            return this;
        }

        public StateConfiguration OnEntryFrom<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, Action<TArg0, TArg1> entryAction)
        {
            return OnEntryFrom<TArg0, TArg1>(trigger, (a0, a1, t) => entryAction(a0, a1));
        }

        public StateConfiguration OnEntryFrom<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, Action<TArg0, TArg1, Transition> entryAction)
        {
            mRepresentation.AddEntryAction(trigger.Trigger, (t, args) => entryAction(
                ParameterConversion.Unpack<TArg0>(args, 0),
                ParameterConversion.Unpack<TArg1>(args, 1), t));
            return this;
        }

        public StateConfiguration OnEntryFrom<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, Action<TArg0, TArg1, TArg2> entryAction)
        {
            return OnEntryFrom<TArg0, TArg1, TArg2>(trigger, (a0, a1, a2, t) => entryAction(a0, a1, a2));
        }

        public StateConfiguration OnEntryFrom<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, Action<TArg0, TArg1, TArg2, Transition> entryAction)
        {
            mRepresentation.AddEntryAction(trigger.Trigger, (t, args) => entryAction(
                ParameterConversion.Unpack<TArg0>(args, 0),
                ParameterConversion.Unpack<TArg1>(args, 1),
                ParameterConversion.Unpack<TArg2>(args, 2), t));
            return this;
        }

        public StateConfiguration OnExit(Action exitAction)
        {
            return OnExit(t => exitAction());
        }

        public StateConfiguration OnExit(Action<Transition> exitAction)
        {
            mRepresentation.AddExitAction(exitAction);
            return this;
        }

        public StateConfiguration SubstateOf(TState superstate)
        {
            StateRepresentation superRepresentation = mLookup(superstate);
            mRepresentation.Superstate = superRepresentation;
            superRepresentation.AddSubstate(mRepresentation);
            return this;
        }

        public StateConfiguration PermitDynamic(TTrigger trigger, Func<TState> destinationStateSelector)
        {
            return PermitDynamicIf(trigger, destinationStateSelector, NoGuard);
        }

        public StateConfiguration PermitDynamic<TArg0>(TriggerWithParameters<TArg0> trigger, Func<TArg0, TState> destinationStateSelector)
        {
            return PermitDynamicIf(trigger, destinationStateSelector, NoGuard);
        }

        public StateConfiguration PermitDynamic<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, Func<TArg0, TArg1, TState> destinationStateSelector)
        {
            return PermitDynamicIf(trigger, destinationStateSelector, NoGuard);
        }

        public StateConfiguration PermitDynamic<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, Func<TArg0, TArg1, TArg2, TState> destinationStateSelector)
        {
            return PermitDynamicIf(trigger, destinationStateSelector, NoGuard);
        }

        public StateConfiguration PermitDynamicIf(TTrigger trigger, Func<TState> destinationStateSelector, Func<bool> guard)
        {
            return InternalPermitDynamicIf(trigger, args => destinationStateSelector(), guard);
        }

        public StateConfiguration PermitDynamicIf<TArg0>(TriggerWithParameters<TArg0> trigger, Func<TArg0, TState> destinationStateSelector, Func<bool> guard)
        {
            return InternalPermitDynamicIf(
                trigger.Trigger,
                args => destinationStateSelector(
                    ParameterConversion.Unpack<TArg0>(args, 0)),
                guard);
        }

        public StateConfiguration PermitDynamicIf<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, Func<TArg0, TArg1, TState> destinationStateSelector, Func<bool> guard)
        {
            return InternalPermitDynamicIf(
                trigger.Trigger,
                args => destinationStateSelector(
                    ParameterConversion.Unpack<TArg0>(args, 0),
                    ParameterConversion.Unpack<TArg1>(args, 1)),
                guard);
        }

        public StateConfiguration PermitDynamicIf<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, Func<TArg0, TArg1, TArg2, TState> destinationStateSelector, Func<bool> guard)
        {
            return InternalPermitDynamicIf(
                trigger.Trigger,
                args => destinationStateSelector(
                    ParameterConversion.Unpack<TArg0>(args, 0),
                    ParameterConversion.Unpack<TArg1>(args, 1),
                    ParameterConversion.Unpack<TArg2>(args, 2)),
                guard);
        }

        private StateConfiguration InternalPermitDynamic(TTrigger trigger, Func<object[], TState> destinationStateSelector)
        {
            return InternalPermitDynamicIf(trigger, destinationStateSelector, NoGuard);
        }

        private StateConfiguration InternalPermitDynamicIf(TTrigger trigger, Func<object[], TState> destinationStateSelector, Func<bool> guard)
        {
            mRepresentation.AddTriggerBehaviour(new DynamicTriggerBehaviour(trigger, destinationStateSelector, guard));
            return this;
        }

        private void EnforceNotIdentityTransition(TState destination)
        {
            if (destination.Equals(mRepresentation.UnderlyingState))
            {
                Debug.LogError("{0} = {1}", destination, mRepresentation.UnderlyingState);
            }
        }
    }
    internal class StateReference
    {
        public TState State { get; set; }
    }
    private IDictionary<TState, StateRepresentation> mStateConfiguration = new Dictionary<TState, StateRepresentation>();
    private IDictionary<TTrigger, TriggerWithParameters> mTriggerConfiguration = new Dictionary<TTrigger, TriggerWithParameters>();
    private Func<TState> mStateAccessor;
    private Action<TState> mStateMutator;
    event Action<Transition> OnTransitionedEvent;

    Action<TState, TTrigger> mUnhandledTriggerAction = DefaultUnhandledTriggerAction;

    static void DefaultUnhandledTriggerAction(TState state, TTrigger trigger)
    {
        Debug.Log("{0} cant trigger {1}", state, trigger);
    }

    public GUISimpleSM(Func<TState> stateAccessor, Action<TState> stateMutator)
    {
        mStateAccessor = stateAccessor;
        mStateMutator = stateMutator;
    }

    public GUISimpleSM(TState initialState)
    {
        var reference = new StateReference { State = initialState };
        mStateAccessor = () => reference.State;
        mStateMutator = s => reference.State = s;
    }

    public TState State
    {
        get
        {
            return mStateAccessor();
        }
        private set
        {
            mStateMutator(value);
        }
    }
    public IEnumerable<TTrigger> PermittedTriggers
    {
        get
        {
            return CurrentRepresentation.PermittedTriggers;
        }
    }

    private StateRepresentation CurrentRepresentation
    {
        get
        {
            return GetRepresentation(State);
        }
    }
    private StateRepresentation GetRepresentation(TState state)
    {
        StateRepresentation result;
        if (!mStateConfiguration.TryGetValue(state, out result))
        {
            result = new StateRepresentation(state);
            mStateConfiguration.Add(state, result);
        }
        return result;
    }
    public StateConfiguration Configure(TState state)
    {
        return new StateConfiguration(GetRepresentation(state), GetRepresentation);
    }

    public void Fire(TTrigger trigger)
    {
        InternalFire(trigger, new object[0]);
    }

    public void Fire<TArg0>(TriggerWithParameters<TArg0> trigger, TArg0 arg0)
    {
        InternalFire(trigger.Trigger, arg0);
    }

    public void Fire<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, TArg0 arg0, TArg1 arg1)
    {
        InternalFire(trigger.Trigger, arg0, arg1);
    }

    public void Fire<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        InternalFire(trigger.Trigger, arg0, arg1, arg2);
    }

    private void InternalFire(TTrigger trigger, params object[] args)
    {
        TriggerWithParameters configuration;
        if (mTriggerConfiguration.TryGetValue(trigger, out configuration))
            configuration.ValidateParameters(args);

        TriggerBehaviour triggerBehaviour;
        if (!CurrentRepresentation.TryFindHandler(trigger, out triggerBehaviour))
        {
            mUnhandledTriggerAction(CurrentRepresentation.UnderlyingState, trigger);
            return;
        }

        var source = State;
        TState destination;
        if (triggerBehaviour.ResultsInTransitionFrom(source, args, out destination))
        {
            var transition = new Transition(source, destination, trigger);
            CurrentRepresentation.Exit(transition);
            State = transition.Destination;
            var onTransitioned = OnTransitionedEvent;
            if (onTransitioned != null)
                onTransitioned(transition);
            CurrentRepresentation.Enter(transition, args);
        }
    }

    public void OnUnhandledTrigger(Action<TState, TTrigger> unhandledTriggerAction)
    {
        if (unhandledTriggerAction != null)
        {
            mUnhandledTriggerAction = unhandledTriggerAction;
        }
    }

    public bool IsInState(TState state)
    {
        return CurrentRepresentation.IsIncludedIn(state);
    }

    public bool CanFire(TTrigger trigger)
    {
        return CurrentRepresentation.CanHandle(trigger);
    }

    public override string ToString()
    {
        List<string> permitList = new List<string>();
        foreach (var tg in PermittedTriggers)
        {
            permitList.Add(tg.ToString());
        }
        return string.Format(
            "StateMachine {{ State = {0}, PermittedTriggers = {{ {1} }}}}",
            State, string.Join(", ", permitList.ToArray()));
    }
    public TriggerWithParameters<TArg0> SetTriggerParameters<TArg0>(TTrigger trigger)
    {
        var configuration = new TriggerWithParameters<TArg0>(trigger);
        SaveTriggerConfiguration(configuration);
        return configuration;
    }

    public TriggerWithParameters<TArg0, TArg1> SetTriggerParameters<TArg0, TArg1>(TTrigger trigger)
    {
        var configuration = new TriggerWithParameters<TArg0, TArg1>(trigger);
        SaveTriggerConfiguration(configuration);
        return configuration;
    }

    public TriggerWithParameters<TArg0, TArg1, TArg2> SetTriggerParameters<TArg0, TArg1, TArg2>(TTrigger trigger)
    {
        var configuration = new TriggerWithParameters<TArg0, TArg1, TArg2>(trigger);
        SaveTriggerConfiguration(configuration);
        return configuration;
    }

    private void SaveTriggerConfiguration(TriggerWithParameters trigger)
    {
        if (!mTriggerConfiguration.ContainsKey(trigger.Trigger))
        {
            mTriggerConfiguration.Add(trigger.Trigger, trigger);
        }
    }

    public void OnTransitioned(Action<Transition> onTransitionAction)
    {
        if (onTransitionAction == null) throw new ArgumentNullException("onTransitionAction");
        OnTransitionedEvent += onTransitionAction;
    }
}
