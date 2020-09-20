import pydicom as dicom
import cv2
import numpy as np
from PIL.Image import fromarray
import png
import csv
import SimpleITK as sitk
from os import listdir, mkdir, rename, remove, path
from os.path import isfile, isdir, join
from radiomics import featureextractor
import pandas as pd
import seaborn as sns
from sklearn.decomposition import PCA
from sklearn.preprocessing import MinMaxScaler
from sklearn.neural_network import MLPClassifier
from sklearn.neighbors import KNeighborsClassifier
from sklearn.svm import SVC
from sklearn import metrics
from joblib import dump, load
import os, sys
from sklearn.metrics import accuracy_score

def MakeDataSets(dataSet):
  patients = np.unique(dataSet['patient'].to_numpy())
  np.random.shuffle(patients)

  trainingPatients, testingPatients = np.split(patients,[int(0.8 * len(patients))])

  trainingDataSet = dataSet[dataSet['patient'].isin(trainingPatients)]
  testingDataSet = dataSet[dataSet['patient'].isin(testingPatients)]

  return trainingDataSet, testingDataSet

def tryClassifier(clf, trainingSetStand, labelsTraining, testingSetStand, labelsTesting):
  #train classifier
  clf.fit(trainingSetStand, labelsTraining['result'])
  clfResultImageTraining = clf.predict_proba(trainingSetStand)
  clfResultImageTraining = clfResultImageTraining[:,1]

  #get threshold for image
  fpr, tpr, thresholds = metrics.roc_curve(labelsTraining['result'].values, clfResultImageTraining, pos_label=1)
  imageThreshold = thresholds[np.argmax(tpr - fpr)]

  #get scores for patients
  patientsTraining = labelsTraining['patient'].to_numpy()
  patientResultsTraining = []
  clfResultPatientTraining = []
  for patientTraining in np.unique(patientsTraining):
    patientResultsTraining.append((labelsTraining[labelsTraining['patient'] == patientTraining]['result'].to_numpy())[0])
    suma = np.sum(clfResultImageTraining[patientsTraining == patientTraining])
    count = len(clfResultImageTraining[patientsTraining == patientTraining])
    clfResultPatientTraining.append(suma/count)

  #get threshold for patients
  fpr, tpr, thresholds = metrics.roc_curve(patientResultsTraining, clfResultPatientTraining, pos_label=1)
  patientThreshold = thresholds[np.argmax(tpr - fpr)]

  #test classifier results
  clfResultImageTesting = clf.predict_proba(testingSetStand)
  clfResultImageTesting = clfResultImageTesting[:,1]
  accuracyImage = accuracy_score(labelsTesting['result'].values, np.where(clfResultImageTesting > imageThreshold, 1,0))
  
  #get testing results for patient
  patientsTesting = labelsTesting['patient'].to_numpy()
  
  patientLablesTesting = []
  clfResultPatientTesting = []
  for patientTesting in np.unique(patientsTesting):
    patientLablesTesting.append((labelsTesting[labelsTesting['patient'] == patientTesting]['result'].to_numpy())[0])
    clfResultPatientTesting.append(sum(clfResultImageTesting[patientsTesting == patientTesting])/ len(clfResultImageTesting[patientsTesting == patientTesting]))

  clfResultPatientTesting = np.where(clfResultPatientTesting < patientThreshold, 0, 1)
  accuracyPatient = accuracy_score(patientLablesTesting, clfResultPatientTesting)

  
  return clf, accuracyImage, imageThreshold, accuracyPatient, patientThreshold

def prepareSetsForClf(trainingSet, testingSet):
  trainingSet = trainingSet.drop(columns = ['photo'])
  testingSet = testingSet.drop(columns = ['photo'])

  # Separating out the features
  featuresTraining = trainingSet[trainingSet.columns.difference(['result', 'patient'])]
  featuresTesting = testingSet[testingSet.columns.difference(['result', 'patient'])]
  # and results
  lablesTraining = trainingSet[['result', 'patient']]
  lablesTesting = testingSet[['result', 'patient']]
  # Standaraizing the features
  scaler = MinMaxScaler()
  trainingSetStand = scaler.fit_transform(featuresTraining)
  testingSetStand = scaler.transform(featuresTesting)
  pca = PCA(0.95)
  pca.fit(trainingSetStand)

  return pca, scaler, pca.transform(trainingSetStand), lablesTraining, pca.transform(testingSetStand), lablesTesting

def testNN(trainingSetStand, labelsTraining, testingSetStand, labelsTesting): 
  settings = []

  maxNeurons = len(pca.explained_variance_ratio_)
  for i in range(1, maxNeurons + 1):
    settings.append((i))
    
  for i in range(1, maxNeurons + 1):
    settings.append((i,i))

  for i in range(1, maxNeurons + 1):
    settings.append((i,i,i))
    
  accPatient = 0
  results = []
  for i in settings:
    bestAcc = 0
    clf = MLPClassifier(alpha=1e-5, hidden_layer_sizes=i, max_iter=100000000, random_state=1, early_stopping=True)

    for j in range(1):
      result = tryClassifier(clf, trainingSetStand, labelsTraining, testingSetStand, labelsTesting)     
      if bestAcc < result[1]:
        bestAcc = result[1]
        bestResult = result

  return bestResult

def testKNN(trainingSetStand, labelsTraining, testingSetStand, labelsTesting):
  maxNeighbours = 100

  settingResultsImage = np.zeros(maxNeighbours)
  settingResultsSumPatient = np.zeros(maxNeighbours)
  settingResultsThreshPatient = np.zeros(maxNeighbours)

  results = []
  maxAuc = 0
  for i in range(maxNeighbours):
    clf = KNeighborsClassifier(n_neighbors=i+1)
    result = tryClassifier(clf, trainingSetStand, labelsTraining, testingSetStand, labelsTesting)
    if maxAuc < result[1]:
      maxAuc = result[1]
      bestResult = result

  return bestResult

def testSVM(trainingSetStand, labelsTraining, testingSetStand, labelsTesting):
  gamma = 0.0001
  logarithimicMaxScale = 8

  results = []
  maxAuc = 0

  for i in range(logarithimicMaxScale):
    c = 0.001
    for j in range(logarithimicMaxScale):
      clf = SVC(gamma=gamma, C=c, probability=True)
      result = tryClassifier(clf, trainingSetStand, labelsTraining, testingSetStand, labelsTesting)
      if maxAuc < result[1]:
        maxAuc = result[1]
        bestResult = result
      c *= 10
    gamma *= 10

  return bestResult

def loadFiles():
  if path.exists(sys.argv[2]):
    databasePath = sys.argv[2]
  else:
    print("*DatabaseMissing*")
    sys.stdout.flush()
    quit(0)
  
  operationMode = sys.argv[3]
  metadataPath = sys.argv[1]
  if operationMode == 'retrain':
    if path.exists(metadataPath):
      metadata = load(metadataPath)
    else:
      print("*MetadataMissing*")
      sys.stdout.flush()
      quit(0)
  else:
    metadata = None  


  return databasePath, metadataPath, metadata, operationMode

databasePath, metadataPath, metadata, operationMode = loadFiles()


dataSet = pd.read_csv(databasePath)

trainingSet, testingSet = MakeDataSets(dataSet)
pca, scaler, trainingSetStand, labelsTraining, testingSetStand, labelsTesting = prepareSetsForClf(trainingSet, testingSet)

if operationMode == 'retrain':
  pca, scaler, clf, imageThreshold, patientThreshold = metadata
  result = tryClassifier(clf, trainingSetStand, labelsTraining, testingSetStand, labelsTesting)
  clf, accuracyImage, imageThreshold, accuracyPatient, patientThreshold = result
else:
  bestResult = testNN(trainingSetStand, labelsTraining, testingSetStand, labelsTesting)
  result = testKNN(trainingSetStand, labelsTraining, testingSetStand, labelsTesting)
  if result[1] > bestResult[1]:
    bestResult = result
  result = testSVM(trainingSetStand, labelsTraining, testingSetStand, labelsTesting)
  if result[1] > bestResult[1]:
    bestResult = result
  clf, accuracyImage, imageThreshold, accuracyPatient, patientThreshold = bestResult

dump((pca, scaler, clf, imageThreshold, patientThreshold), metadataPath)
