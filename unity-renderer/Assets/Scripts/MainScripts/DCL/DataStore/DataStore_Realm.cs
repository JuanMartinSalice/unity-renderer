﻿using Variables.RealmsInfo;

namespace DCL
{
    public class DataStore_Realm
    {
        public readonly CurrentRealmVariable playerRealm = new CurrentRealmVariable();
        public readonly RealmsVariable realmsInfo = new RealmsVariable();
    }
}