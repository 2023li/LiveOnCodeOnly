//#define EVENTROUTER_THROWEXCEPTIONS 
#if EVENTROUTER_THROWEXCEPTIONS
//#define EVENTROUTER_REQUIRELISTENER // Uncomment this if you want listeners to be required for sending events.
#endif

using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;

namespace Moyo.Unity
{
    /// <summary>
    /// MoyoGameEvents are used throughout the game for general game events (game started, game ended, life lost, etc.)
    /// 游戏事件在整个游戏中用于一般游戏事件（游戏开始、游戏结束、生命损失等）。
    /// </summary>
    public struct MoyoGameEvent
	{
		static MoyoGameEvent e;
		
		public string EventName;
		public int IntParameter;
		public Vector2 Vector2Parameter;
		public Vector3 Vector3Parameter;
		public bool BoolParameter;
		public string StringParameter;
		
		public static void Trigger(string eventName, int intParameter = 0, Vector2 vector2Parameter = default(Vector2), Vector3 vector3Parameter = default(Vector3), bool boolParameter = false, string stringParameter = "")
		{
			e.EventName = eventName;
			e.IntParameter = intParameter;
			e.Vector2Parameter = vector2Parameter;
			e.Vector3Parameter = vector3Parameter;
			e.BoolParameter = boolParameter;
			e.StringParameter = stringParameter;
			MoyoEventManager.TriggerEvent(e);
		}
	}

    /// <summary>
    /// 这个类负责事件管理，可用于在整个游戏中广播事件，告知一个（或多个）类发生了某些事情。
    /// 事件是结构体，你可以定义任何你想要的事件类型。这个管理器自带 MoyoGameEvents，基本上只是由一个字符串组成，但如果你愿意，也可以使用更复杂的事件。
    ///
    /// 要在任何地方触发一个新事件，执行 YOUR_EVENT.Trigger (YOUR_PARAMETERS)
    /// 例如，MoyoGameEvent.Trigger ("Save"); 将触发一个名为 Save 的 MoyoGameEvent 事件
    ///
    /// 你也可以调用 MoyoEventManager.TriggerEvent (YOUR_EVENT);
    /// 例如：MoyoEventManager.TriggerEvent (new MoyoGameEvent ("GameStart")); 将向所有监听器广播一个名为 GameStart 的 MoyoGameEvent 事件。
    ///
    /// 要从任何类开始监听一个事件，你必须做三件事：
    ///
    /// 1 - 声明你的类为该类型的事件实现了 IMoyoEventListener 接口。
    /// 例如：public class GUIManager : Singleton<GUIManager>, IMoyoEventListener<MoyoGameEvent>
    /// 你可以有多个这样的声明（每种事件类型一个）。
    ///
    /// 2 - 在启用和禁用时，分别开始和停止监听该事件：
    ///void OnEnable ()
    /// {
    /// this.MoyoEventStartListening<MoyoGameEvent>();
    /// }
    /// void OnDisable()
    /// {
    /// this.MoyoEventStopListening<MoyoGameEvent>();
    /// }
    ///
    /// 3 - 为该事件实现 IMoyoEventListener 接口。例如：
    ///public void OnMoyoEvent (MoyoGameEvent gameEvent)
    /// {
    /// if (gameEvent.EventName == "GameOver")
    /// {
    /// // 执行某些操作
    /// }
    /// }
    /// 这将捕获游戏中任何地方发出的所有 MoyoGameEvent 类型的事件，如果事件名为 GameOver 则执行某些操作。
    /// </summary>
    [ExecuteAlways]
	public static class MoyoEventManager 
	{
		private static Dictionary<Type, List<MoyoEventListenerBase>> _subscribersList;
		
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void InitializeStatics()
		{
			_subscribersList = new Dictionary<Type, List<MoyoEventListenerBase>>();
		}

		static MoyoEventManager()
		{
			_subscribersList = new Dictionary<Type, List<MoyoEventListenerBase>>();
		}

		/// <summary>
		/// Adds a new subscriber to a certain event.
		/// </summary>
		/// <param name="listener">listener.</param>
		/// <typeparam name="MoyoEvent">The event name.</typeparam>
		public static void AddListener<MoyoEvent>( IMoyoEventListener<MoyoEvent> listener ) where MoyoEvent : struct
		{
			Type eventType = typeof( MoyoEvent );

			if (!_subscribersList.ContainsKey(eventType))
			{
				_subscribersList[eventType] = new List<MoyoEventListenerBase>();
			}

			if (!SubscriptionExists(eventType, listener))
			{
				_subscribersList[eventType].Add( listener );
			}
		}

		/// <summary>
		/// Removes a subscriber from a certain event.
		/// </summary>
		/// <param name="listener">listener.</param>
		/// <typeparam name="MoyoEvent">The event name.</typeparam>
		public static void RemoveListener<MoyoEvent>( IMoyoEventListener<MoyoEvent> listener ) where MoyoEvent : struct
		{
			Type eventType = typeof( MoyoEvent );

			if( !_subscribersList.ContainsKey( eventType ) )
			{
				#if EVENTROUTER_THROWEXCEPTIONS
					throw new ArgumentException( string.Format( "Removing listener \"{0}\", but the event type \"{1}\" isn't registered.", listener, eventType.ToString() ) );
				#else
				return;
				#endif
			}

			List<MoyoEventListenerBase> subscriberList = _subscribersList[eventType];

			#if EVENTROUTER_THROWEXCEPTIONS
	            bool listenerFound = false;
			#endif

			for (int i = subscriberList.Count-1; i >= 0; i--)
			{
				if( subscriberList[i] == listener )
				{
					subscriberList.Remove( subscriberList[i] );
					#if EVENTROUTER_THROWEXCEPTIONS
					    listenerFound = true;
					#endif

					if ( subscriberList.Count == 0 )
					{
						_subscribersList.Remove(eventType);
					}						

					return;
				}
			}

			#if EVENTROUTER_THROWEXCEPTIONS
		        if( !listenerFound )
		        {
					throw new ArgumentException( string.Format( "Removing listener, but the supplied receiver isn't subscribed to event type \"{0}\".", eventType.ToString() ) );
		        }
			#endif
		}

		/// <summary>
		/// Triggers an event. All instances that are subscribed to it will receive it (and will potentially act on it).
		/// </summary>
		/// <param name="newEvent">The event to trigger.</param>
		/// <typeparam name="MoyoEvent">The 1st name parameter.</typeparam>
		public static void TriggerEvent<MoyoEvent>( MoyoEvent newEvent ) where MoyoEvent : struct
		{
			List<MoyoEventListenerBase> list;
			if( !_subscribersList.TryGetValue( typeof( MoyoEvent ), out list ) )
				#if EVENTROUTER_REQUIRELISTENER
			            throw new ArgumentException( string.Format( "Attempting to send event of type \"{0}\", but no listener for this type has been found. Make sure this.Subscribe<{0}>(EventRouter) has been called, or that all listeners to this event haven't been unsubscribed.", typeof( MMEvent ).ToString() ) );
				#else
				return;
			#endif
			
			for (int i=list.Count-1; i >= 0; i--)
			{
				( list[i] as IMoyoEventListener<MoyoEvent> ).OnMoyoEvent( newEvent );
			}
		}

		/// <summary>
		/// Checks if there are subscribers for a certain name of events
		/// </summary>
		/// <returns><c>true</c>, if exists was subscriptioned, <c>false</c> otherwise.</returns>
		/// <param name="type">Type.</param>
		/// <param name="receiver">Receiver.</param>
		private static bool SubscriptionExists( Type type, MoyoEventListenerBase receiver )
		{
			List<MoyoEventListenerBase> receivers;

			if( !_subscribersList.TryGetValue( type, out receivers ) ) return false;

			bool exists = false;

			for (int i = receivers.Count-1; i >= 0; i--)
			{
				if( receivers[i] == receiver )
				{
					exists = true;
					break;
				}
			}

			return exists;
		}
	}

	/// <summary>
	/// Static class that allows any class to start or stop listening to events
	/// 静态扩展类运行任何类去监听一个事件
	/// </summary>
	public static class EventRegister
	{
		public delegate void Delegate<T>( T eventType );

		public static void MoyoEventStartListening<EventType>( this IMoyoEventListener<EventType> caller ) where EventType : struct
		{
			MoyoEventManager.AddListener<EventType>( caller );
		}

		public static void MoyoEventStopListening<EventType>( this IMoyoEventListener<EventType> caller ) where EventType : struct
		{
          
            MoyoEventManager.RemoveListener<EventType>( caller );
		}
	}

	/// <summary>
	/// Event listener basic interface
	/// </summary>
	public interface MoyoEventListenerBase { };

	/// <summary>
	/// A public interface you'll need to implement for each name of event you want to listen to.
	/// </summary>
	public interface IMoyoEventListener<T> : MoyoEventListenerBase
	{
		void OnMoyoEvent( T eventType );
	}

	public class MoyoEventListenerWrapper<TOwner, TTarget, TEvent> : IMoyoEventListener<TEvent>, IDisposable
		where TEvent : struct
	{
		private Action<TTarget> _callback;

		private TOwner _owner;
		public MoyoEventListenerWrapper(TOwner owner, Action<TTarget> callback)
		{
			_owner = owner;
			_callback = callback;
			RegisterCallbacks(true);
		}

		public void Dispose()
		{
			RegisterCallbacks(false);
			_callback = null;
		}

		protected virtual TTarget OnEvent(TEvent eventType) => default;
		public void OnMoyoEvent(TEvent eventType)
		{
			var item = OnEvent(eventType);
			_callback?.Invoke(item);
		}

		private void RegisterCallbacks(bool b)
		{
			if (b)
			{
				this.MoyoEventStartListening<TEvent>();
			}
			else
			{
				this.MoyoEventStopListening<TEvent>();
			}
		}
	}
}
