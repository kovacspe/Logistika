using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using MinHeap;

namespace ConsoleApplication1
{
    interface Solver
    {
        State ResolveCity(State initialState, City c, Model m, bool movesOnly);
        State SolvePlanes(State initialState, Model m,bool movesOnly);
        
    }

    class IterativeDeepenigSolver:Solver
    {
        public State ResolveCity(State initialState, City c, Model m, bool movesOnly)
        {
            return Program.IterativeDeepeningAStar(initialState, m, (State s, Model mo) => { return s.IsCityInBestPosition(c, mo); },
                (int i) => m.places[i].city == c, false, true, c.id,movesOnly);
        }

        public State SolvePlanes(State initialState, Model m, bool movesOnly)
        {
            return Program.IterativeDeepeningAStar(initialState, m, (State s, Model mo) => { return s.IsEveryPackageInRightCity(mo); },
                (int i) => true, true, false, -1,movesOnly);
        }
    }

   

    class Program
    {
        
        struct Solution
        {
            public List<string> instructions;
            public int cost;
            public State finalState;
            public Solution(State s,List<string> instr, int cost)
            {
                this.cost = cost;
                instructions = instr;
                finalState = s;
            }
        }


        static State GlobalSolver(Model m, bool movesOnly)
        {
            return IterativeDeepeningAStar(m.initialState, m, (State s, Model mo) => { return s.IsFinal(mo, (int i) => true); },
                (int i) => true, true, true, -1, movesOnly);
        }
       

        static State LocalSolver(Model m, bool movesOnly)
        {
            Solver s = new IterativeDeepenigSolver();
            // resolve cities
            State currentState = m.initialState;
            //currentState.PrintState(Console.Out, m);
            foreach(var city in m.cities)
            {
                //Console.WriteLine("Solving city {0}", city.Value.id);
                currentState = s.ResolveCity(currentState,city.Value, m, movesOnly);
                //currentState.PrintState(Console.Out, m);
                //foreach(var line in currentState.WrapInstructions())
                //    Console.WriteLine(line);
            }

            Console.WriteLine("Planes");
            currentState = s.SolvePlanes(currentState,m,movesOnly);
            //currentState.PrintState(Console.Out, m);
            
            foreach (var city in m.cities)
            {
                //Console.WriteLine("Solving city {0}", city.Value.id);
                currentState = s.ResolveCity(currentState, city.Value, m,movesOnly);
                //currentState.PrintState(Console.Out, m);
            }
            return currentState;
        }


        public static State IterativeDeepeningAStar(State initialState,Model m, 
            Func<State, Model, bool> IsFinal,Func<int,bool> restriction, bool planes, bool trucks, int cityID, bool movesOnly)
        {
           
            HashSet<State> exploredStates = new HashSet<State>();
            int limit = State.f(initialState, m, restriction, planes, trucks, cityID);
            
            Console.WriteLine("INIT LIMIT: {0}",limit);

            while (true)
            {
                Solution solution = IDA(initialState, m, limit, exploredStates,IsFinal,restriction,planes,trucks,cityID,movesOnly);
                if (solution.instructions != null && solution.finalState != null)
                {
                    //solution.finalState.PrintState(Console.Out, m);
                    return solution.finalState;
                }
                else if (solution.cost == int.MaxValue)
                {
                    return solution.finalState;
                }
                limit = solution.cost;

                exploredStates = new HashSet<State>();
            }
        }

        private static Solution IDA(State currState, Model m, int limit, HashSet<State> exploredStates, 
            Func<State, Model, bool> IsFinal,Func<int,bool> restriction, bool planes, bool trucks, int cityID, bool movesOnly)
        {
           
            if (IsFinal(currState,m))
                return new Solution(currState,currState.WrapInstructions(), currState.currentPrice);
            if (State.f(currState, m, restriction, planes, trucks, cityID) > limit)
                return new Solution(null, null, State.f(currState, m, restriction, planes, trucks, cityID));
            List<State> succesors;
            if (movesOnly) succesors = currState.ExpandStateMovesOnly(m, planes, trucks, cityID);
            else succesors = currState.ExpandState(m, planes, trucks, cityID);
            succesors = succesors.OrderBy(s => (s.GetHeuristicValue(m, restriction, planes, trucks, cityID) + s.currentPrice)).ToList();
            exploredStates.Add(currState);
            if (succesors.Count == 0) return new Solution(null, null, limit);
            int newLimit = int.MaxValue;
            bool everySuccesorHasMaxValue = false;
            foreach (State s in succesors)
            {
                //  s.PrintState(Console.Out, m);
                if (exploredStates.Contains(s)) continue;
                var result = IDA(s, m, limit, exploredStates, IsFinal, restriction, planes, trucks, cityID, movesOnly);
                if (result.instructions != null)
                {
                    return result;
                }                
                newLimit = Math.Min(newLimit, result.cost);
                
            }
            
            return new Solution(null,null, newLimit);
        }

        static State AStar(State initialState, Model m, Func<State,Model,bool> IsFinal,Func<int,bool> restriction, bool planes, bool trucks, int cityID)
        {
            HashSet<State> exploredStates = new HashSet<State>();
            MinHeap<State, int> heap = new MinHeap<State, int>();
            heap.Insert(initialState, 0);

            exploredStates.Add(initialState);
            bool stop = false;
            while (heap.Count != 0)
            {
                State s = heap.ExtractMin().Key;
                var states = s.ExpandState(m,planes,trucks,cityID);
                //Console.WriteLine(counter);
                foreach (State state in states)
                {
                    if (IsFinal(state,m))
                    {
                        return state;
                    }

                    if (!exploredStates.Contains(state))
                    {
                        heap.Insert(state, state.GetHeuristicValue(m,restriction,planes,trucks,cityID) + state.currentPrice);
                        exploredStates.Add(state);
                    }

                }
            }
            return null;

        }

        static void OutputState(State state,string resultFile, string logFile, string method,double time)
        {
            
            StreamWriter swResult = new StreamWriter(method+"_"+resultFile);
            StreamWriter swLog = new StreamWriter(logFile,true);
            if (state == null)
            {
                swLog.WriteLine("{0};{1};{2};{3}", resultFile, method, time, "NO_SOLUTION_FOUND");
            }
            else
            {
                Solution solution = new Solution(state, state.WrapInstructions(), state.currentPrice);
                if (solution.instructions == null)
                {
                    Console.WriteLine("NO SOLUTION FOUND");
                    return;
                }
                foreach (string line in solution.instructions)
                {
                    swResult.WriteLine(line);

                }
                swLog.WriteLine("{0};{1};{2};{3}", resultFile, method, time, state.currentPrice);
            }
            swResult.Close();
            swLog.Close();
        }

        static void Main(string[] args)
        {
            string logFile = "log.txt";
            Model model = null;
            if (args.Length < 2) return;
            try
            {
                model = ParseModel(args[0]);
            }
            catch (IOException)
            {
                Console.WriteLine("Cannot read file: {0}", args[0]);
                return;
            }
            catch (FormatException)
            {
                Console.WriteLine("Error during parsing input file");
                return;
            }


            //List<string> lines = AStar(model.initialState, model);
            State state;
            //LOCAL BMoves only
            Console.WriteLine("LOCAL_MOVESONLY");
            DateTime start = DateTime.Now;
            state = LocalSolver(model, true);
            OutputState(state, args[1], logFile, "LOCAL_MOVESONLY", (DateTime.Now - start).Milliseconds);
            //LOCAL 
            Console.WriteLine("LOCAL_ALL");
            start = DateTime.Now;
            state = LocalSolver(model, false);
            OutputState(state, args[1], logFile, "LOCAL_ALLACTIONS", (DateTime.Now - start).Milliseconds);
            //GLOBAL BEST
            Console.WriteLine("GLOBAL_MOVESONLY");
            start = DateTime.Now;
            state = GlobalSolver(model, true);
            OutputState(state, args[1], logFile, "GLOBAL_MOVESONLY", (DateTime.Now - start).Milliseconds);
            //GLOBAL IDA
            Console.WriteLine("GLOBAL_ALL");
            start = DateTime.Now;
            state = GlobalSolver(model, false);
            OutputState(state, args[1], logFile, "GLOBAL_ALLACTIONS", (DateTime.Now - start).Milliseconds);




        }

        static string ReadLine(StreamReader sr)
        {
            string s = sr.ReadLine();
            while (s == "" || s[0] == '%')
            {
                s = sr.ReadLine();
            }
            return s;
        }

        static Model ParseModel(string filename)
        {
            Model model = new Model();
            StreamReader sr = new StreamReader(filename);
            int numberOfCities = int.Parse(ReadLine(sr));
            int numberOfPlaces = int.Parse(ReadLine(sr));
            for (int i = 0; i < numberOfCities; i++)
            {
                model.cities.Add(i, new City(i));
            }

            for (int i = 0; i < numberOfPlaces; ++i)
            {
                int cityID = int.Parse(ReadLine(sr));
                City city = model.cities[cityID];
                Place placeToAdd = new Place(i, city,city.places.Count);
                model.places.Add(i, placeToAdd);
                city.places.Add(placeToAdd);

            }
            // airports
            for (int i = 0; i < numberOfCities; i++)
            {
                int placeID = int.Parse(ReadLine(sr));
                Place p = model.places[placeID];
                p.isAirport = true;
                p.city.airport = p;
                model.Airports.Add(p);
            }

            // trucks
            int numberOfTrucks = int.Parse(ReadLine(sr));
            Place[] truckPositions = new Place[numberOfTrucks];
            for (int i = 0; i < numberOfTrucks; i++)
            {
                truckPositions[i] = model.places[int.Parse(ReadLine(sr))];
                truckPositions[i].city.TruckIDs.Add(i);
            }

            //planes
            int numberOfPlanes = int.Parse(ReadLine(sr));
            Place[] planesPositions = new Place[numberOfPlanes];
            for (int i = 0; i < numberOfPlanes; i++)
            {
                planesPositions[i] = model.places[int.Parse(ReadLine(sr))];
            }

            //packages
            int numberOfPackages = int.Parse(ReadLine(sr));
            List<Package>[] PackagesOnPositions = new List<Package>[numberOfPlaces];
            for (int i = 0; i < numberOfPlaces; i++)
            {
                PackagesOnPositions[i] = new List<Package>();
            }
            for (int i = 0; i < numberOfPackages; i++)
            {
                string[] info = ReadLine(sr).Split(' ');
                int startPosition = int.Parse(info[0]);
                Place target = model.places[int.Parse(info[1])];
                Package package = new Package(i, target);
                PackagesOnPositions[startPosition].Add(package);
                model.packages.Add(i, package);

            }

            State initialState = new State(truckPositions, planesPositions, PackagesOnPositions);
            initialState.currentPrice = 0;
            model.initialState = initialState;

            return model;
        }
    }
    class Model
    {
        public static int LoadTruckPrice = 2;
        public static int UnloadTruckPrice = 2;
        public static int TruckMovePrice = 17;
        public static int LoadPlanePrice = 14;
        public static int UnloadPlanePrice = 11;
        public static int PlaneMovePrice = 120;
        public static int PlaneEffectivePrice = 4;
        public static int TruckEffectivePrice = 4;
        public static int PlaneCapacity = 30;
        public static int TruckCapacity = 4;
        public static int TruckFullServicePrice = LoadTruckPrice + TruckEffectivePrice + UnloadTruckPrice;
        public static int PlaneFullServicePrice = LoadPlanePrice + PlaneEffectivePrice + UnloadPlanePrice;

        public List<Place> Airports;
        public Dictionary<int, Place> places;
        public Dictionary<int, City> cities;
        public Dictionary<int, Package> packages;

        public State initialState;

        public Model()
        {

            Airports = new List<Place>();
            places = new Dictionary<int, Place>();
            cities = new Dictionary<int, City>();
            packages = new Dictionary<int, Package>();
        }
    }

    class City
    {
        public int id;
        public List<Place> places;
        public List<int> TruckIDs;
        public Place airport;
        public City(int id)
        {
            this.id = id;
            airport = null;
            places = new List<Place>();
            TruckIDs = new List<int>();
        }

    }

    class Place
    {
        public int id;
        public City city;
        public int CityID;
        public bool isAirport;


        public Place(int id, City city,int cityID)
        {
            this.id = id;
            this.isAirport = false;
            this.city = city;
            this.CityID = cityID;
        }
    }

    class Package
    {
        public int id;
        public Place Target;
        public Place TargetInCity(City c)
        {
            if (Target.city == c) return Target;
            else return c.airport;
        }

        public Package(int id, Place target)
        {
            this.id = id;
            Target = target;
        }
    }

    class State
    {

        public State predecessor;
        public int currentPrice;
        private int heuristicValue=-1;
        public string instructionAppliedOnPredecessor;
        Place[] PlaneAtPlace;
        Place[] TruckAtPlace;
        List<Package>[] PackagesOnTruck;
        List<Package>[] PackagesOnPlane;
        List<Package>[] PackagesOnPlace;

        public State(Place[] trucksPosition, Place[] planesPosition, List<Package>[] packagesOnPLaces)
        {

            PlaneAtPlace = planesPosition;
            TruckAtPlace = trucksPosition;
            PackagesOnTruck = new List<Package>[trucksPosition.Length];
            for (int i = 0; i < PackagesOnTruck.Length; i++)
            {
                PackagesOnTruck[i] = new List<Package>();
            }
            PackagesOnPlane = new List<Package>[planesPosition.Length];
            for (int i = 0; i < PackagesOnPlane.Length; i++)
            {
                PackagesOnPlane[i] = new List<Package>();
            }
            PackagesOnPlace = packagesOnPLaces;
        }

        public State(State s, string instruction, int instructionCost)
        {

            PlaneAtPlace = new Place[s.PlaneAtPlace.Length];
            s.PlaneAtPlace.CopyTo(PlaneAtPlace, 0);
            TruckAtPlace = new Place[s.TruckAtPlace.Length];
            s.TruckAtPlace.CopyTo(TruckAtPlace, 0);
            PackagesOnPlace = new List<Package>[s.PackagesOnPlace.Length];
            for (int i = 0; i < s.PackagesOnPlace.Length; i++)
            {
                PackagesOnPlace[i] = new List<Package>(s.PackagesOnPlace[i]);
            }

            PackagesOnTruck = new List<Package>[s.PackagesOnTruck.Length];
            for (int i = 0; i < s.PackagesOnTruck.Length; i++)
            {
                PackagesOnTruck[i] = new List<Package>(s.PackagesOnTruck[i]);
            }

            PackagesOnPlane = new List<Package>[s.PackagesOnPlane.Length];
            for (int i = 0; i < s.PackagesOnPlane.Length; i++)
            {
                PackagesOnPlane[i] = new List<Package>(s.PackagesOnPlane[i]);
            }
            instructionAppliedOnPredecessor = instruction;
            currentPrice = s.currentPrice + instructionCost;
            predecessor = s;
            
        }

        public static int f(State s, Model m,Func<int,bool> restriction, bool  planes, bool trucks, int cityID)
        {
            return s.currentPrice + s.GetHeuristicValue(m,restriction,planes,trucks,cityID);
        }

        public void WrapInstructions(List<string> instructions)
        {
            if (predecessor != null)
            {
                predecessor.WrapInstructions(instructions);
                instructions.Add(instructionAppliedOnPredecessor);
            }
        }

        public List<string> WrapInstructions()
        {
            List<string> instructions = new List<string>();
            if (predecessor != null)
                predecessor.WrapInstructions(instructions);
            instructions.Add(instructionAppliedOnPredecessor);
            return instructions;

        }

        public bool IsEveryPackageInRightCity(Model m)
        {
            int numOfPackages=0;
            // packages on places
            for(int i=0;i<PackagesOnPlace.Length;++i)
            {
                foreach(Package p in PackagesOnPlace[i])
                {
                    numOfPackages++;
                    if (p.Target.city != m.places[i].city) return false;
                }
            }

            // check if every package is on ground
            foreach (var list in PackagesOnPlane)
            {
                if (list.Count != 0) return false;
            }


                return true;

        }

        public bool IsCityInBestPosition(City city,Model m)
        {
            foreach (var place in city.places)
            {
                //packages on places
                foreach (Package package in PackagesOnPlace[place.id])
                {
                    if(package.Target!=place && !(package.Target.city!=city && place.isAirport))
                    {
                        return false;
                    }
                }

               

            }
  
                //check trucks in city
            for (int i = 0; i < TruckAtPlace.Length;++i )
            {
                if (TruckAtPlace[i].city==city)
                {
                    if (PackagesOnTruck[i].Count != 0) return false;
                }
            }

                //check planes
            for (int i = 0; i < PlaneAtPlace.Length; ++i)
            {
                if (PlaneAtPlace[i].city == city)
                {
                    if (PackagesOnPlane[i].Count != 0) return false;
                }
            }


                return true;

        }

        public bool IsFinal(Model model,Func<int,bool> restriction)
        {
            return GetHeuristicValue(model,restriction) == 0;
        }

        private bool PlaceIsEmpty(Place p)
        {
            return PackagesOnPlace[p.id].Count == 0;
        }

        private bool IsInTruckTargets(int truckId, Place p)
        {
            foreach (var pack in PackagesOnTruck[truckId])
            {
                if (pack.Target == p) return true;
                if (pack.Target.city!=TruckAtPlace[truckId].city && p.isAirport) return true;
            }
            return false;
        }

        private bool IsInPlaneTargets(int planeId, Place p)
        {
            foreach (var pack in PackagesOnPlane[planeId])
            {
                if (pack.Target.city == p.city) return true;
            }
            return false;
        }

        public void PrintState(TextWriter tw, Model m)
        {
            tw.WriteLine("---------------------------------------------------\nPOPIS STAVU");
            tw.WriteLine("Po akcii: {0}", instructionAppliedOnPredecessor);
            tw.WriteLine("Cena stavu: {0}", currentPrice);
            tw.WriteLine("Heuristicky odhad: {0}", GetHeuristicValue(m, (int i) => { return true; }));
            tw.WriteLine("\nPOLOHA DODAVEK");
            for (int i = 0; i < TruckAtPlace.Length; i++)
            {
                tw.WriteLine("{0} is on {1}", i, TruckAtPlace[i].id);
                tw.Write("\tLoad: ");
                foreach (var item in PackagesOnTruck[i])
                {
                    tw.Write("{0}, ", item.id);
                }
                tw.WriteLine();
            }

            tw.WriteLine("\nPOLOHA LETADEL");
            for (int i = 0; i < PlaneAtPlace.Length; i++)
            {
                tw.WriteLine("{0} is on {1}", i, PlaneAtPlace[i].id);
                tw.Write("\tLoad: ");
                foreach (var item in PackagesOnPlane[i])
                {
                    tw.Write("{0}, ", item.id);
                }
                tw.WriteLine();
            }

            tw.WriteLine("\nBALIKY NA MISTECH");
            for (int i = 0; i < PackagesOnPlace.Length; i++)
            {
                string aero = "";
                if (m.places[i].isAirport) aero = "AIRPORT";
                tw.WriteLine("On place {0} {1} in city {2} is :", i,aero,m.places[i].city.id);
                foreach (var item in PackagesOnPlace[i])
                {
                    tw.Write("{0}, ", item.id);
                }
                tw.WriteLine();
            }
        }

        private static bool _PackagesEquals(List<Package>[] l1, List<Package>[] l2)
        {
            for (int i = 0; i < l1.Length; ++i)
            {
                if (l1[i].Count != l2[i].Count) return false;
                for (int j = 0; j < l1[i].Count; j++)
                {
                    if (!l2[i].Contains(l1[i][j])) return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is State)) return false;
            State s = (State)obj;

            for (int i = 0; i < PlaneAtPlace.Length; ++i)
            {
                if (s.PlaneAtPlace[i] != PlaneAtPlace[i]) return false;
            }

            for (int i = 0; i < TruckAtPlace.Length; ++i)
            {
                if (s.TruckAtPlace[i] != TruckAtPlace[i]) return false;
            }

            if (!_PackagesEquals(PackagesOnPlace, s.PackagesOnPlace)) return false;
            if (!_PackagesEquals(PackagesOnPlane, s.PackagesOnPlane)) return false;
            if (!_PackagesEquals(PackagesOnTruck, s.PackagesOnTruck)) return false;

            return true;
        }

        private static int _GetHashCodeOfArray(Place[] places)
        {
            unchecked
            {
                int hash = 17;
                foreach (Place p in places)
                {
                    hash = hash * 23 + p.id;
                }
                return hash;
            }
        }

        private static int _GetHashCodeOfPackages(List<Package>[] l)
        {
            unchecked
            {
                int hash = 11;
                foreach (var list in l)
                {
                    int sum = 0;
                    foreach (Package p in list)
                    {
                        sum = sum + p.id*p.id;
                    }
                    hash = hash * 17 + sum;
                }
                return hash;
            }

        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 19;

                hash = hash * 31 + _GetHashCodeOfArray(TruckAtPlace);
                hash = hash * 31 + _GetHashCodeOfArray(PlaneAtPlace);
                hash = hash * 31 + _GetHashCodeOfPackages(PackagesOnPlace);
                hash = hash * 31 + _GetHashCodeOfPackages(PackagesOnPlane);
                hash = hash * 31 + _GetHashCodeOfPackages(PackagesOnTruck);
                return hash;
            };
        }



        public List<State> ExpandStateMovesOnly(Model m, bool allowPlanes, bool allowTrucks, int allowedCity)
        {
            List<State> expandedStates = new List<State>();
            if (allowPlanes)
            {
                // foreach plane
                for (int i = 0; i < PlaneAtPlace.Length; ++i)
                {
                    // foreach destination
                    Place bestDestination = null;
                    int bestUtility = -1;
                    foreach(Place dest in m.Airports)
                    {
                        if (dest == PlaneAtPlace[i]) continue;
                        // count utility ... number of packages, which can transfer
                        int utility = 0;
                        foreach(Package p in PackagesOnPlace[PlaneAtPlace[i].id])
                        {
                            if (p.Target.city==dest.city)
                            {
                                utility += 100;
                            }
                        }

                        foreach (Package p in PackagesOnPlace[dest.id])
                        {
                            if (p.Target.city != dest.city)
                            {
                                utility += 1;
                            }
                        }

                        //save if its best
                        if (utility>bestUtility)
                        {
                            bestUtility = utility;
                            bestDestination = dest;
                        }
                    }

                    if (bestDestination == null) continue;
                    State nextState = this;

                    // load packages with goal destination
                    for (int j = PackagesOnPlace[PlaneAtPlace[i].id].Count-1; j >= 0;j--)
                    {
                        Package p = PackagesOnPlace[PlaneAtPlace[i].id][j];
                        if (p.Target.city == bestDestination.city)
                            nextState = nextState.LoadPackageOnPlane(p, i);
                        if (nextState.PackagesOnTruck[i].Count >= 30) break;
                    }

                    
                    // move plane
                    nextState = nextState.MovePlane(i, bestDestination);
                    // add state to list
                    Console.WriteLine("STATE_COST {0}", nextState.currentPrice);
                    expandedStates.Add(nextState);
                    
                    
                }
            }

            if (allowTrucks)
            {
                for (int i = 0; i < TruckAtPlace.Length; i++)
                {
                    if (allowedCity!=-1 && TruckAtPlace[i].city.id != allowedCity)
                    {                 
                        continue;
                    }
                    Place bestDestination = null;
                    int bestUtility = -1;
                    // foreach destination
                    foreach (Place dest in TruckAtPlace[i].city.places)
                    {
                        if (dest == TruckAtPlace[i]) continue;
                        // count utility ... number of packages, which can transfer
                        int utility = 0;
                        foreach (Package p in PackagesOnPlace[TruckAtPlace[i].id])
                        {
                            if (p.Target == dest || (p.Target.city != dest.city && dest.isAirport))
                            {
                                utility += 100;
                            }
                        }
                        foreach (Package p in PackagesOnPlace[dest.id])
                        {
                            if (p.Target != dest && !(p.Target.city != dest.city && dest.isAirport))
                            {
                                utility += 1;
                            }
                        }
                        //foreach (Package p )
                        //save if its best
                        if (utility > bestUtility)
                        {
                            bestUtility = utility;
                            bestDestination = dest;
                        }
                    }
                    if (bestDestination == null) continue;
                    
                    State nextState = this;
                    //unload unnecessary
                    /*for (int j = PackagesOnTruck[i].Count - 1; j >= 0; j--)
                    {
                        Package p = PackagesOnTruck[i][j];
                        if (p.Target != bestDestination && !(p.Target.city != bestDestination.city && bestDestination.isAirport))
                            nextState = nextState.UnLoadPackageFromTruck(p, i);

                    }*/

                        // load packages with goal destination
                    for (int j = PackagesOnPlace[TruckAtPlace[i].id].Count - 1; j >= 0; j--)
                    {
                        if (nextState.PackagesOnTruck[i].Count >= 4) break;
                        Package p = PackagesOnPlace[TruckAtPlace[i].id][j];
                        if (p.Target == bestDestination || (p.Target.city!=bestDestination.city && bestDestination.isAirport))
                            nextState = nextState.LoadPackageOnTruck(p, i);
                        
                    }

                    // move plane
                    nextState = nextState.MoveTruck(i, bestDestination);


                    // add state to list
                    expandedStates.Add(nextState);
                    //Console.WriteLine("bestAction is move truck {0} from {1} to {2}", i, TruckAtPlace[i].id, bestDestination.id);
                }
            }
            return expandedStates;
        }

        // allowedCity is -1 when all cities should be concidered
        public List<State> ExpandState(Model m, bool allowPlanes, bool allowTrucks, int allowedCity)
        {
            List<State> expandedStates = new List<State>();

            //plane actions
            if (allowPlanes)
            {
                for (int i = 0; i < PlaneAtPlace.Length; ++i)
                {
                    //plane load
                    if (PackagesOnPlane[i].Count < Model.PlaneCapacity)
                        foreach (Package p in PackagesOnPlace[PlaneAtPlace[i].id])
                        {
                            if (p.Target.city != PlaneAtPlace[i].city)
                                expandedStates.Add(LoadPackageOnPlane(p, i));
                        }
                    //plane unload
                    foreach (Package p in PackagesOnPlane[i])
                    {
                        expandedStates.Add(UnLoadPackageFromPlane(p, i));
                    }

                    // plane move
                    foreach (Place p in m.Airports)
                    {
                        if ((p != PlaneAtPlace[i]) && (!PlaceIsEmpty(p) || IsInPlaneTargets(i, p)) 
                            && (PackagesOnPlane[i].Count != Model.PlaneCapacity) || IsInPlaneTargets(i, p))
                        {
                            expandedStates.Add(MovePlane(i, p));
                        }
                    }
                }
            }

            //truck actions
            if (allowTrucks)
            {
                for (int i = 0; i < TruckAtPlace.Length; ++i)
                {
                    if (TruckAtPlace[i].city.id != allowedCity && allowedCity != -1) continue;

                    //truck load
                    if (PackagesOnTruck[i].Count < Model.TruckCapacity)
                        foreach (Package p in PackagesOnPlace[TruckAtPlace[i].id])
                        {
                            if (!TruckAtPlace[i].isAirport || (p.Target.city == TruckAtPlace[i].city))
                                expandedStates.Add(LoadPackageOnTruck(p, i));
                        }
                    //truck unload
                    foreach (Package p in PackagesOnTruck[i])
                    {
                        expandedStates.Add(UnLoadPackageFromTruck(p, i));
                    }

                    // truck move
                    foreach (Place p in TruckAtPlace[i].city.places)
                    {
                        if (!(p == TruckAtPlace[i]) && (!PlaceIsEmpty(p) || IsInTruckTargets(i, p))
                            && (PackagesOnTruck[i].Count != Model.TruckCapacity) || IsInTruckTargets(i, p))
                        {
                            expandedStates.Add(MoveTruck(i, p));
                        }
                    }
                }
            }



            return expandedStates;
        }

        public State MoveTruck(int TruckID, Place p)
        {
            State newState = new State(this, "drive " + TruckID + " " + p.id, Model.TruckMovePrice);
            newState.TruckAtPlace[TruckID] = p;

            //automatically unload all packages which are in target position
            for (int j = PackagesOnTruck[TruckID].Count - 1; j >= 0; j--)
            {
                Package package = PackagesOnTruck[TruckID][j];
                if (package.Target == p)
                {

                    newState = newState.UnLoadPackageFromTruck(package, TruckID);
                    // not interesting for the task from this point

                    newState.PackagesOnPlace[p.id].Remove(package);
                }
                else if (package.Target.city != p.city && p.isAirport)
                {
                    newState = newState.UnLoadPackageFromTruck(package, TruckID);

                }

            }

            return newState;
        }

        public State MovePlane(int PlaneID, Place p)
        {
            State newState = new State(this, "fly " + PlaneID + " " + p.id, Model.PlaneMovePrice);
            newState.PlaneAtPlace[PlaneID] = p;

            //automatically unload all packages which are in target position
            
            for(int j= PackagesOnPlane[PlaneID].Count-1;j>=0;j--)
            {
                Package package = PackagesOnPlane[PlaneID][j];
                if (package.Target.city == p.city)
                {

                    newState = newState.UnLoadPackageFromPlane(package, PlaneID);

                    if (package.Target == p)
                    {
                        // not interesting for the task from this point
                        newState.PackagesOnPlace[p.id].Remove(package);
                    }
                }
            }

            return newState;
        }

        public State LoadPackageOnTruck(Package package, int TruckID)
        {
            State newState = new State(this, "load " + TruckID + " " + package.id, Model.LoadTruckPrice);
            
            newState.PackagesOnPlace[TruckAtPlace[TruckID].id].Remove(package);
            newState.PackagesOnTruck[TruckID].Add(package);
            return newState;
        }


        public State UnLoadPackageFromTruck(Package package, int TruckID)
        {
            State newState = new State(this, "unload " + TruckID + " " + package.id, Model.UnloadTruckPrice);
            newState.PackagesOnTruck[TruckID].Remove(package);
            newState.PackagesOnPlace[TruckAtPlace[TruckID].id].Add(package);
          
            return newState;
        }

        public State LoadPackageOnPlane(Package package, int PlaneID)
        {
            State newState = new State(this, "pickUp " + PlaneID + " " + package.id, Model.LoadPlanePrice);
            newState.PackagesOnPlace[PlaneAtPlace[PlaneID].id].Remove(package);
            newState.PackagesOnPlane[PlaneID].Add(package);
          
            return newState;
        }

        public State UnLoadPackageFromPlane(Package package, int PlaneID)
        {
            State newState = new State(this, "dropOff " + PlaneID + " " + package.id, Model.UnloadPlanePrice);
            newState.PackagesOnPlane[PlaneID].Remove(package);
            newState.PackagesOnPlace[PlaneAtPlace[PlaneID].id].Add(package);
           
            return newState;
        }



        public int GetHeuristicValue(Model model, Func<int, bool> restriction, bool planes=true, bool trucks=true, int cityID=-1) 
        {
            //if (heuristicValue >= 0) return heuristicValue;
            int sum = 0;
            CostCounter counter = new CostCounter(model);
            counter.AddPackagesByPlaces(PackagesOnPlace, i => i,restriction);
            int numOfPackagesOnTruck = counter.AddPackagesByPlaces(PackagesOnTruck, i => TruckAtPlace[i].id,restriction);
            int numOfPackagesOnPlane = counter.AddPackagesByPlaces(PackagesOnPlane, i => PlaneAtPlace[i].id,restriction);

            sum += counter.countCost(planes, trucks, cityID) 
                - numOfPackagesOnPlane*(Model.LoadPlanePrice) 
                - numOfPackagesOnTruck*(Model.LoadTruckPrice);

            heuristicValue = sum;
            return sum;
        }


    }

    class CostCounter
    {
        TransferMatrix cityMatrix;
        TransferMatrix[] placeMatrices;
        Model model;

        public CostCounter(Model m)
        {
            model = m;
            cityMatrix = new TransferMatrix(null,m.cities.Count);
            placeMatrices = new TransferMatrix[m.cities.Count];
            for (int i = 0; i < m.cities.Count; i++)
            {
                placeMatrices[i] = new TransferMatrix(m.cities[i],m.cities[i].places.Count);
            }
        }

        public int countCost( bool planes, bool trucks, int cityID)
        {
            int cost = 0;
            if (planes)
                cost += cityMatrix.countCost(Model.PlaneMovePrice,Model.LoadPlanePrice+Model.UnloadPlanePrice,Model.PlaneCapacity);
            //Console.WriteLine("Cost of planes is {0}", cost);
            if (trucks)
                foreach(TransferMatrix city in placeMatrices)
                {
                    if (city.CityReference.id == cityID || cityID == -1)
                    {

                        int c =  city.countCost(Model.TruckMovePrice, Model.LoadTruckPrice + Model.UnloadTruckPrice, Model.TruckCapacity);
                        //Console.WriteLine("Cost of city {0} is {1}",city.CityReference.id,c);
                        cost += c;
                    }
                }
            return cost;
        }
        public int AddPackagesByPlaces(List<Package>[] packageLocationArray, Func<int, int> placeIndexer,Func<int,bool> restriction)
        {
            int processedPackages = 0;
            for (int i = 0; i < packageLocationArray.Length; i++)
            {
                if (restriction(placeIndexer(i)))
                    foreach (Package p in packageLocationArray[i])
                    {

                        Place currPlace = model.places[placeIndexer(i)];
                        cityMatrix.Increment(currPlace.city.id, p.Target.city.id);
                        if (currPlace.city == p.Target.city)
                            placeMatrices[p.Target.city.id].Increment(currPlace.CityID, p.Target.CityID);
                        else
                        {
                            placeMatrices[currPlace.city.id].Increment(currPlace.CityID, currPlace.city.airport.CityID);
                            placeMatrices[p.Target.city.id].Increment(p.Target.city.airport.CityID, p.Target.CityID);
                        }
                        processedPackages++;
                    }
            }
            return processedPackages;
        }



    }

    struct Transfer
    {
        Place from;
        Place to;
        int amount;
    }

    struct MoveDescription
    {
        public int VehicleID;
        List<Transfer> Changes;
    }

    class TransferMatrixStateRepresentation
    {
        TransferMatrix cityMatrix;
        TransferMatrix[] placeMatrices;
        Model model;
        public List<string> Actions {get; private set;}

        public void ModifyToBestAction() 
        { 
            
        }

    }


    class TransferMatrix
    {
        int[,] _transferMatrix;
        int size;
        public City CityReference;


        public TransferMatrix(City city,int numberOfPlaces)
        {
            _transferMatrix = new int[numberOfPlaces,numberOfPlaces];
            size = numberOfPlaces;
            CityReference = city;
        }




        //return number of processed packages
        public void Increment(int i, int j)
        {
            _transferMatrix[i, j]++;
        }



        public int countCost(int moveCost,int serviceCost, int capacity)
        {
            int cost = 0;
            int serviceCosts=0;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    if (i != j)
                    {
                        cost += (_transferMatrix[i, j] + capacity - 1) / capacity;
                        serviceCosts += _transferMatrix[i, j] * serviceCost;
                    }

                }
            }
            return cost*moveCost+serviceCost;
        }
        


    }
}
