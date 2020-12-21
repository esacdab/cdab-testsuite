/*
cdab-client is part of the software suite used to run Test Scenarios 
for bechmarking various Copernicus Data Provider targets.
    
Copyright (C) 2020 Terradue Ltd, www.terradue.com
    
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

/*
cdab-client is part of the software suite used to run Test Scenarios 
for bechmarking various Copernicus Data Provider targets.
    
Copyright (C) 2020 Terradue Ltd, www.terradue.com
    
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Target;
using log4net;
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;
using Terradue.ServiceModel.Ogc.Eop21;
using Terradue.ServiceModel.Syndication;

namespace cdabtesttools.TestCases
{
    internal class TestCase303 : TestCase301
    {
        public TestCase303(ILog log, TargetSiteWrapper target, int load_factor, List<IOpenSearchResultItem> foundItems) :
            base(log, target, foundItems)
        {
            Id = "TC303";
            Title = "Bulk Remote Download";
            base.load_factor = load_factor * target.TargetSiteConfig.MaxDownloadThread;
        }

        protected override List<IOpenSearchResultItem> SearchForMoreItemsToDownload()
        {
            return null;
        }
    }
}
