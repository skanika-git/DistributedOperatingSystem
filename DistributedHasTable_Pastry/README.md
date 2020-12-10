# Project 3 – Pastry

Kanika Sharma | UFID : 7119-1343 <br />

## Instructions to run the program :
1. Unzip project3.zip
2. Ensure project3.fsx is included in the compile in .fsproj file
3. Through the command line, run the program using the command :
dotnet fsi --langversion:preview project3.fsx numNodes numRequests
(providing different values for numNodes and numRequests)

## Working Part :

<ul>
<li>The protocol is implemented as per the Pastry paper. Pastry APIs described are called within the program.</li>
<li>The program runs correctly as per requirements</li>
<li>As per the parameter 'numNodes' passed, nodes get added to the network. Each second the system starts to request based on the value passed for 'numRequests' for each of the nodes. Each of these requests are routed correctly to the node which has its id numerically closest to the given key requested.</li>
<li>The program terminates exactly when all the nodes have made the given number of requests and have calculated total hops it took for each request.</li>  
<li>As per the requirements, the average number of hops or node connections is calculated and printed on to the console.</li>
<li>All the requirements for the project are met and we have successfully implemented the protocol.</li>
</ul>

## Largest network managed to deal with :

Largest network that could be managed consisted of 10,000 peers. For 10,000 nodes, when each peer made 10 requests, we obtained average number of hops as 3.3388 ms.

Below is a table showing the average number of hops we obtained for different values of numNodes and numRequests :   
   
numNodes  | numRequests  | average number of hops
------------- | ------------- | -------------
10  | 5  | 0.9
100  | 5  | 1.58
1000  | 5  | 2.51
10000  | 5  | 3.323
10000  | 10  | 3.3388
