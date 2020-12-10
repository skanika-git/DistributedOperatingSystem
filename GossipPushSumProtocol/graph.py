import os

topo = ["line","full", "2D", "imp2D"]
proto = ["gossip","push-sum"]

def name(p, t):
	return "out_"+p+"_"+t+".txt"

for t in topo:
	for p in proto:
		for n in range(100, 1000, 100):
			command = "dotnet fsi --langversion:preview hmm.fsx "+ str(n) + " " + t + " " + p + ">>" + name(p,t)
			os.system(command)

from matplotlib import pyplot as plt
for algo in proto:
	for t in topo:
		file = name(algo,t)
		x = range(100, 1000, 100)
		y = []
		for line in open(file, "r"):
			y.append(int(line.strip()))
		plt.plot(x,y, label=t)
	plt.title(algo)
	plt.ylabel("Time in ms")
	plt.xlabel("Number of nodes")
	plt.legend()
	plt.show()