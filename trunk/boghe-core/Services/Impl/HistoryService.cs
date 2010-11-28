﻿/*
* Boghe IMS/RCS Client - Copyright (C) 2010 Mamadou Diop.
*
* Contact: Mamadou Diop <diopmamadou(at)doubango.org>
*	
* This file is part of Boghe Project (http://code.google.com/p/boghe)
*
* Boghe is free software: you can redistribute it and/or modify it under the terms of 
* the GNU General Public License as published by the Free Software Foundation, either version 3 
* of the License, or (at your option) any later version.
*	
* Boghe is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
* without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  
* See the GNU General Public License for more details.
*	
* You should have received a copy of the GNU General Public License along 
* with this program; if not, write to the Free Software Foundation, Inc., 
* 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using BogheCore.Model;
using System.Xml.Serialization;
using System.IO;
using BogheCore.Events;
using System.Timers;

namespace BogheCore.Services.Impl
{
    public class HistoryService : IHistoryService
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(HistoryService));

        private const String FILE_NAME = "history.xml";
        private readonly ServiceManager manager;

        private MyObservableCollection<HistoryEvent> events;
        private bool loading;
        private readonly Timer deferredSaveTimer;
        private readonly XmlSerializer xmlSerializer;

        public HistoryService(ServiceManager manager)
        {
            this.manager = manager;
            this.loading = false;

            this.deferredSaveTimer = new Timer(2500);
            this.deferredSaveTimer.AutoReset = false;
            this.deferredSaveTimer.Elapsed += delegate
            {
                //this.manager.Dispatcher.Invoke((System.Threading.ThreadStart)delegate
                //{
                    this.ImmediateSave();
                //}, null);
            };
            this.xmlSerializer = new XmlSerializer(typeof(MyObservableCollection<HistoryEvent>));
        }

        #region IService

        public bool Start()
        {
            new System.Threading.Thread((System.Threading.ThreadStart)this.LoadHistory).Start();

            return true;
        }

        public bool Stop()
        {
            if (this.deferredSaveTimer.Enabled)
            {
                this.deferredSaveTimer.Stop();
                this.ImmediateSave();
            }
            return true;
        }

        #endregion


        #region IHistoryService

        public bool IsLoading 
        {
            get { return this.loading; }
        }

        public MyObservableCollection<HistoryEvent> Events 
        {
            get { return this.events; }
        }

        public void AddEvent(HistoryEvent @event)
        {
            this.events.Insert(0, @event);

            HistoryEventArgs eargs = new HistoryEventArgs(HistoryEventTypes.ADDED);
            eargs.AddExtra("event", @event);
            EventHandlerTrigger.TriggerEvent<HistoryEventArgs>(this.onHistoryEvent, this, eargs);

            this.DeferredSave();
        }

        public void UpdateEvent(HistoryEvent @event)
        {

        }

        public void DeleteEvent(HistoryEvent @event)
        {
            this.events.Remove(@event);

            HistoryEventArgs eargs = new HistoryEventArgs(HistoryEventTypes.REMOVED);
            eargs.AddExtra("event", @event);
            EventHandlerTrigger.TriggerEvent<HistoryEventArgs>(this.onHistoryEvent, this, eargs);

            this.DeferredSave();
        }

        public void DeleteEvent(int location)
        {
            if (location < 0 || location >= this.events.Count)
            {
                LOG.Error("Index OutOfRange");
                return;
            }

            HistoryEvent @event = this.events[location];
            this.DeleteEvent(@event);
        }

        public void Clear()
        {
            this.events.Clear();
            EventHandlerTrigger.TriggerEvent<HistoryEventArgs>(this.onHistoryEvent, this, new HistoryEventArgs(HistoryEventTypes.RESET));
            this.DeferredSave();
        }

        public event EventHandler<HistoryEventArgs> onHistoryEvent;

        #endregion

        private void DeferredSave()
        {
            this.deferredSaveTimer.Stop();
            this.deferredSaveTimer.Start();
        }

        private bool ImmediateSave()
        {
            lock (this.events)
            {
                LOG.Debug("Saving history...");
                try
                {
                    using (StreamWriter writer = new StreamWriter(HistoryService.FILE_NAME))
                    {
                        this.xmlSerializer.Serialize(writer, this.events);
                        writer.Flush();
                        writer.Close();
                    }
                    return true;
                }
                catch (IOException ioe)
                {
                    LOG.Error("Failed to save history", ioe);
                }
            }
            return false;
        }

        private void LoadHistory()
        {
            this.loading = true;

            LOG.Debug(String.Format("Loading history from {0}", HistoryService.FILE_NAME));

            try
            {
                if (!File.Exists(HistoryService.FILE_NAME))
                {
                    LOG.Debug(String.Format("{0} doesn't exist, trying to create new one", HistoryService.FILE_NAME));
                    File.Create(HistoryService.FILE_NAME).Close();

                    // create xml declaration
                    this.events = new MyObservableCollection<HistoryEvent>();
                    this.ImmediateSave();
                }

                using (StreamReader reader = new StreamReader(HistoryService.FILE_NAME))
                {
                    try
                    {
                        this.events = this.xmlSerializer.Deserialize(reader) as MyObservableCollection<HistoryEvent>;
                    }
                    catch (InvalidOperationException ie)
                    {
                        LOG.Error("Failed to load history", ie);

                        reader.Close();
                        File.Delete(HistoryService.FILE_NAME);
                    }
                }
            }
            catch (Exception e)
            {
                LOG.Error("Failed to load history", e);
            }

            this.loading = false;

            EventHandlerTrigger.TriggerEvent<HistoryEventArgs>(this.onHistoryEvent, this, new HistoryEventArgs(HistoryEventTypes.RESET));
        }
    }
}