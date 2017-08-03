﻿using JetBrains.Annotations;
using LiteDB;

namespace Remembrance.DAL.Contracts.Model
{
    public abstract class Entity
    {
        [UsedImplicitly, BsonIndex(true)]
        public int Id { get; set; }

        public bool IsNew()
        {
            return Id == 0;
        }
    }
}