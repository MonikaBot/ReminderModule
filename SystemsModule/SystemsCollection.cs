using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonikaBot.Commands;
using Newtonsoft.Json;
using System.Linq;
using DSharpPlus.Entities;
using System.Globalization;
using System.Linq;

namespace SystemsModule
{
    /// <summary>
    /// Systems collection.
    /// 
    /// The accepted syntax is 
    /* 
    /// SystemsCollection<string, string>
    ///                   ^name   ^system
    */
    /// </summary>
    public class SystemsCollection<TKey, TValue> : Dictionary<TKey, TValue> 
    {
        /// <summary>
        /// The unique Discord snowflake of the user who owns these systems.
        /// </summary>
        public ulong UserSnowflake = 0;

        public SystemsCollection():base()
        {
        }

        public SystemsCollection(int capacity):base(capacity) 
        {
            
        }
    }
}
