import subprocess
import sys
import pkg_resources

required = {'numpy', 'pydicom', 'opencv-python', 'simpleitk', 'joblib', 'pandas', 'pyradiomics'}
installed = {pkg.key for pkg in pkg_resources.working_set}
missing = required - installed

if missing:
    python = sys.executable
    subprocess.check_call([python, '-m', 'pip', 'install', *missing], stdout=subprocess.DEVNULL)

import pydicom as dicom
import cv2
import numpy as np
import csv
import SimpleITK as sitk
from os import walk, path
from radiomics import featureextractor
import pandas as pd
from joblib import dump, load
import os
import ntpath

def convertDCMtoArray(imageDICOM):
  image = imageDICOM.pixel_array * imageDICOM.RescaleSlope + imageDICOM.RescaleIntercept
  image = (((image - image.min())  / (image.max() - image.min())) * 255).astype(np.uint8)
  return image

def indexOfValidContours(cnts, h):
    ignore = []

    if not hasattr(h, 'min'):
        return ignore

    for i in range(0, len(h[0])):
        curr = h[0][i]

    if cv2.contourArea(cnts[i]) < 200 and cv2.contourArea(cnts[i]) > 2000 and not np.any(np.isin(ignore, i)):
      ignore = np.append(ignore, i)
      #set my Next's previous to my previous
      if curr[0] != -1:
        h[0][curr[0]][1] = curr[1]
      #set my Previous' next to my next
      if curr[1] != -1:
        h[0][curr[1]][0] = curr[0]
      #set my parent child to my next
      if curr[3] != -1 and h[0][curr[3]][2] == i:
        h[0][curr[3]][2] = curr[0]
      #children will be even smaller so also ignored
      for c in range(0, len(h[0])):
        if h[0][c][3] == i:
          ignore = np.append(ignore, c)

    result = np.delete(range(0, len(cnts)), ignore)
    return np.delete(range(0, len(cnts)), ignore)

def getNRRDs(imageArray):
  #find threshold for which we are 99% certain to find only bones 
  blur = cv2.GaussianBlur(imageArray,(7,7),0)
  th = cv2.threshold(blur[blur > np.average(blur)], 0, 255, cv2.THRESH_BINARY+cv2.THRESH_OTSU)[0]
  brighterTissue = cv2.threshold(blur, th, 255, cv2.THRESH_TOZERO)[1]
  
  kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(5,5))
  brighterTissuePercentage = 1

  while brighterTissuePercentage > 0.25:
    #As bones do not usualy excide 25% of the body area this means that we got some other tissue
    #Even if bone area is small <0.10 other tissue inclusion almost always spikes the area above 25%
    #in a case where bones excide 25% we loose only small area doing this again
    th = cv2.threshold(brighterTissue[brighterTissue > np.average(brighterTissue)], 0, 255, cv2.THRESH_BINARY+cv2.THRESH_OTSU)[0]
    brighterTissue = cv2.threshold(blur, th, 255, cv2.THRESH_TOZERO)[1]
    
    #remove "noisy holes"
    brighterTissue = cv2.morphologyEx(brighterTissue, cv2.MORPH_CLOSE, kernel)    
    brighterTissuePercentage = np.count_nonzero(np.where((brighterTissue > 20), 1, 0)) / max(np.count_nonzero(np.where((blur > 20), 1, 0)), 1)
  
  #make 1/0 mask 
  brighterTissue = cv2.threshold(blur, th, 255, cv2.THRESH_BINARY)[1]

  #remove "holes" in the bones
  kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(10,10))
  brighterTissue = cv2.morphologyEx(brighterTissue, cv2.MORPH_CLOSE, kernel)

  #remove noise
  kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(3,3))
  brighterTissue = cv2.morphologyEx(brighterTissue, cv2.MORPH_OPEN, kernel)
  
  
  #add mask to blur to aid canny edge detection
  #only masked area will be picked as strong edge
  #weak edges will be added only if connected to strong one, result imposible with just thresholding
  blur = cv2.add(blur, brighterTissue)     
  canny = cv2.Canny(blur,th * 0.65,300)
  

  #expand bone edges, merging those that are close together
  #this stops contour finding from separating 1 bone into 2 as there was 1mm gap in the edges
  kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(7,7))
  canny = cv2.dilate(canny, kernel)

  cnts, h = cv2.findContours(canny, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)
  

  #find contors that are right size for a bone and are not contouring cavity - inside of a skull 
  validIndexes = indexOfValidContours(cnts, h)

  notBoneMask = np.zeros((imageArray.shape[0],imageArray.shape[1]), np.uint8)
  boneMask = np.zeros((imageArray.shape[0],imageArray.shape[1]), np.uint8)
  boneLabel = 255

  for i in range(0, len(cnts)):    
    #if valid contour
    if np.any(np.isin(validIndexes, i)):
      #if inside another contour
      if h[0][i][2] == -1:

        mask = np.zeros((imageArray.shape[0],imageArray.shape[1]), np.uint8)
        mask = cv2.drawContours(mask, cnts, i, boneLabel, cv2.FILLED)
        #if area is much dimmer than bones
        if np.average(blur[mask == boneLabel]) < th + 20:
          notBoneMask = cv2.add(notBoneMask, mask)
      #if not inside another contour
      if h[0][i][3] == -1:
        boneMask = cv2.drawContours(boneMask, cnts, i, boneLabel, cv2.FILLED)

  boneMask = cv2.subtract(boneMask, notBoneMask)

  #move mask and image to format understood by RADIOMICS
  boneMask = sitk.GetImageFromArray(boneMask, isVector=False)
  image = sitk.GetImageFromArray(imageArray, isVector =False)

  return image, boneMask, boneLabel

def extractFeatures(extractor, filePath):
    try:
        imageDicom = dicom.dcmread(filePath)
    except:
        return None
    imageArray = convertDCMtoArray(imageDicom)
    image, mask, label = getNRRDs(imageArray)
    extractor.settings['force2D'] = True
    if sitk.GetArrayFromImage(mask).max() == label:
        fd = os.open(os.devnull,os.O_WRONLY)
        savefd = os.dup(2)
        os.dup2(fd,2)
    
        features = extractor.execute(image, mask, label=label)
        os.dup2(savefd,2)

        featureList = []
        for key, value in features.items():
            if key.startswith('original'):
                featureList.append(float(value))
        return featureList

def prepareFeatures(pca, scaler, features): 
    features = np.array(features) 
    features = features.reshape(1, -1)
    features = scaler.transform(features)
    features = pca.transform(features)
    return features

def saveFeatures(databasePath, imagesFeatures, patientClass):
  data = pd.read_csv(databasePath)
  currentPatient = data["patient"].max() + 1

  with open(databasePath, 'a', newline='') as databaseCSV:
    writerCSV = csv.writer(databaseCSV, dialect='excel')
    for imageFeatures in imagesFeatures:
      result = []
      result.append(currentPatient)
      result.append(imageFeatures[0])     
      result.append(patientClass)
      for feature in imageFeatures[1]:
        result.append(feature)
      writerCSV.writerow(result)

def classifyImage(featuresNormalised, clf, imageThreshold):
    imageScore = clf.predict_proba(featuresNormalised)[:,1][0]
    imageResult = 1 if imageScore > imageThreshold else 0
    return imageResult, imageScore

def classifyPatient(imageScores, patientThreshold):
  r = sum(imageScores)
  patientScore = sum(imageScores) / len(imageScores)
  patientResult = 1 if patientScore > patientThreshold else 0
  return patientResult, patientScore

def calculateProbabilityForUser(clfProbability, threshold):
  if clfProbability > threshold:
    return 0.5 + ((clfProbability - threshold) / (1 - threshold))/2
  else:
    return 0.5 + ((threshold - clfProbability) / threshold)/2

def getAbsoluteFilePathsFromFolder(folderPath):
  absolutePaths = []
  for folderPath,_,fileNames in walk(folderPath):
    for fileName in fileNames:
      absolutePaths.append(path.abspath(path.join(folderPath, fileName)))
  return absolutePaths

def loadFiles():
  if not path.exists(sys.argv[1]): 
    print("*DICOMFolderMissing*")
    sys.stdout.flush()
    quit(0)
  
  filesPaths = getAbsoluteFilePathsFromFolder(sys.argv[1])
  if len(filesPaths) == 0:
    print("*DICOMFolderEmpty*")
    sys.stdout.flush()
    quit(0)
  
  if path.exists(sys.argv[2]):
    databasePath = sys.argv[2]
  else:
    print("*DatabaseMissing*")
    sys.stdout.flush()
    quit(0)
  
 
  patientClass = None
  if sys.argv[3] == 'positive' or sys.argv[3] == 'negative':
      patientClass = sys.argv[3]

  if patientClass == None:
    try:
      metadata = load(sys.argv[3])
    except:
      print("*MetadataMissing*")
      sys.stdout.flush()
      quit(0)
  else:
    metadata = None

  return filesPaths, databasePath, patientClass, metadata

filesPaths, databasePath, patientClass, metadata = loadFiles()

if patientClass is not None:
    featureSet = []
    extractor = featureextractor.RadiomicsFeatureExtractor()
    for filePath in filesPaths:
        result = extractFeatures(extractor, filePath)
        if result is not None:
            featureSet.append((ntpath.basename(filePath), result))
    saveFeatures(databasePath, featureSet, 1 if 'positive' in patientClass else 0)
    quit(0)

pca, scaler, clf, imageThreshold, patientThreshold = metadata
imageResults = []
featureSet = []
extractor = featureextractor.RadiomicsFeatureExtractor()
for filePath in filesPaths:
    features = extractFeatures(extractor, filePath)
    if features is not None:
        featureSet.append((ntpath.basename(filePath), features))
        preparedFeatures = prepareFeatures(pca, scaler, features)
        imageResult = classifyImage(preparedFeatures, clf, imageThreshold)
        imageResults.append(imageResult)
        probability = calculateProbabilityForUser(imageResult[1], imageThreshold)
        print(f'{ntpath.basename(filePath)};{"positive" if imageResult[0] == 1 else "negative"};{probability};')
        sys.stdout.flush()
    else:        
        print(f'{ntpath.basename(filePath)};{"negative"};0.5;')
    sys.stdout.flush()


patientResult = classifyPatient(np.array(imageResults)[:,1], patientThreshold)
probability = calculateProbabilityForUser(patientResult[1], patientThreshold)
print(f'{"positive" if patientResult[0] == 1 else "negative"};{probability};;')
sys.stdout.flush()

for line in sys.stdin:
  if line == 'none':
    quit(0)
  else:
    saveFeatures(databasePath, featureSet, 1 if 'positive' in line else 0)
    quit(0)
