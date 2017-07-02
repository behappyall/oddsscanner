﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using BetsLibrary;

namespace ArbitrageService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract]
    public interface IArbitrageService
    {

        [OperationContract]
        List<ArbitrageBet> GetArbitrageList();
        

        // TODO: Add your service operations here
    }

    
}
