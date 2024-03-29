﻿using System;
using System.ComponentModel;

namespace Scripting
{
    public abstract class ScriptableObject : INotifyPropertyChanged
    {
        public readonly string ID;
        internal readonly Type ScriptableObjectType;

        public ScriptableObject(string id)
        {
            ID = id.ToLower();
            ScriptableObjectType = this.GetType();
        }

        public abstract event PropertyChangedEventHandler PropertyChanged;
    }
}