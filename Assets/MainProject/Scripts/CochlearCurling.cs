using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Diagnostics;
// https://answers.unity.com/questions/550262/how-to-follow-curved-path.html
// https://answers.unity.com/questions/392606/line-drawing-how-can-i-interpolate-between-points.html
// https://www.reddit.com/r/gamedev/comments/96f8jl/if_you_are_making_an_rpg_you_need_to_know_the/
// https://danielilett.com/2019-09-08-unity-tips-3-interpolation/

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CochlearCurling : MonoBehaviour
{
    #region variables
    public bool getData = false;
    [Header("Geometry settings")]
    public float scale = 1.0f;
    [Range(3,100)]public int nGon = 3;
    // private float stepSize = 0.1f;
    public float stepSize = 25f;
    public bool evenDistribution = false;
   

    [Header("Insertion Settings")]
    public bool manualInsertion;
    public bool automateInsertion;

    public bool automateInsertionWithSpatial;
    public bool automateNonlinearInsertion;
    public bool  automateRetraction;
    public bool ThreeDCurl;
    [Range(0f,360f)] public float electrodeRoll = 0f;
    [Range(-100f,100f)]public float electrodeInsertionSpeed = 0f;
    [Range(10f,100f)]public float timeToCurl = 20f; 
    [Range(-19f,19f)] public float angleOfInsertion = 0f;

    [Range(0f,1f)] public float interp = 1f; //for interpolation of angle and pos
     private float previousInterp, modifiedInterp;

    [Header("Curling Modification")]
    [Range(0,100)]public int criticalPoint;
    [Range(0,2.5f)]public float curlingModifier = 0f;
    

    //Saving Data
    [HideInInspector] public List<float> linearPosOutput,linearVelOutput,rotaryPosOutput,timeOutput;
    [HideInInspector] public Stopwatch myTimer;
    
   

    //For mesh
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private int total,oneSideTri;
    private int numPoints;
    //Hit Detection
    private Quaternion qAngle,insertionAngle;
    private Vector3 hitDirection;
    private float distInner, distOuter;
   
    //Defined from (Clark et.al, 2011)
    private float R,theta,theta_mod;
    private float s= 3f,A= 3.762f,B= 0.001317f,C= 7.967f,D= 0.1287f,E= 0.003056f;
    private float THETA_0 = 5f,THETA_1 = 10.3f;
    // private float THETA_END = 480f;
    private float THETA_END = 380f;
    // private float THETA_END = 910.3f;
    private float cochlearLength, totalLength;

    //Unrolling Mesh
    private Vector3 basePoint,origin,planeNormal;
    private Vector3[] endPos,varPos;
    private Quaternion[] varRotations,endRotations, otherVarRotations;
    private Quaternion varRot,startRot,accumulate,currentToPrev, otherVarRot;
    private Vector3 pivot,prevDir,currentDir,currentDefinePos,prevDefinePos,currentCurlingPoint,prevCurlingPoint,nGonPoint;
    private float rotatePolygonPlane;
    private GameObject springMassObject, finalMassObject, pivotParent;
    private GameObject[] massList;
    private int layerMask;
    private Collider[] hitColliders;
    private Vector3 scaleVector;
    private float previousAngleOfInsertion;

    private int numSteps;

    private Vector3 velocityVec;
    private Vector3 previousPositionForVec, startingPosition;

    private float wallScalar = 2.0f, prevWallScalar = 2.0f, bestInterp = 1.0f;
    private float finalMassPos;

    public bool testingArticulated;

    private ArticulationJointType myJoint;

    private Vector3 forceDir;

    

    #endregion
    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        mesh.MarkDynamic();
        Time.fixedDeltaTime = 0.02f; //0.02 Seconds
        layerMask = 1 << 3; //3 is the layer number for the cochlear model
        scaleVector = new Vector3(scale,scale,scale);
        
        numPoints = (int)Mathf.Ceil((THETA_END-THETA_1)/stepSize);
        
        prevDir = Vector3.up;
        // origin = GameObject.Find("StartingPoint").transform.localPosition;
        endRotations = new Quaternion[numPoints-1];
        varRotations = new Quaternion[numPoints-1];
        otherVarRotations = new Quaternion[numPoints-1];

        endPos = new Vector3[numPoints];
        varPos = new Vector3[numPoints];

        massList = new GameObject[numPoints];
        
        total = nGon*numPoints;
        oneSideTri = nGon*(numPoints-1)*6; 

        vertices = new Vector3[total];
        triangles = new int[oneSideTri*2];

        DefineCochlear();   

        // Move top to bottom so that insertion can start correctly
        // this.transform.Translate(new Vector3(0,-cochlearLength,0));

        previousInterp = interp;  

        numSteps = 0;

        // this.transform.RotateAround(finalMassObject.transform.position,Vector3.forward,angleOfInsertion - previousAngleOfInsertion);
        this.transform.RotateAround(massList[massList.Length-1].transform.position,Vector3.forward,angleOfInsertion - previousAngleOfInsertion);
        previousAngleOfInsertion = angleOfInsertion;

        myTimer = new Stopwatch();

        myTimer.Start();

        this.transform.position  = new Vector3(0,-30.3f,0);


        velocityVec = new Vector3(0,1,0);

        previousPositionForVec = this.transform.position;

        startingPosition = this.transform.position;
        UnityEngine.Debug.Log(startingPosition);

        this.gameObject.AddComponent<Rigidbody>();
        this.gameObject.GetComponent<Rigidbody>().drag = 1f;
        this.gameObject.GetComponent<Rigidbody>().useGravity = false;
    }



    void DefineCochlear(){
        for (int i=0;i<numPoints;i++){
            theta = THETA_1+stepSize*i;
            if (theta<=99.9){
                R = C*(1-D*Mathf.Log(theta-THETA_0));
            }
            else{
                theta_mod = 0.0002f*theta*theta+.98f*theta;
                R = A*Mathf.Exp(-B*theta_mod);
            }

            if (ThreeDCurl){
                currentDefinePos = new Vector3(-s*R*Mathf.Sin(theta*Mathf.PI/180f),-s*R*Mathf.Cos(theta*Mathf.PI/180f),s*E*(theta-THETA_1));

            }
            else{
                currentDefinePos = new Vector3(-s*R*Mathf.Sin(theta*Mathf.PI/180f),-s*R*Mathf.Cos(theta*Mathf.PI/180f),0);
            }

            if (i==0){
                origin = currentDefinePos;
            }
            currentDefinePos -= origin;


            endPos[i] = currentDefinePos;

            if (i == numPoints-1){
                // finalMassObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                finalMassObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                finalMassObject.name = "TopMass";
                finalMassObject.transform.localScale = 2f*scaleVector;
                // finalMassObject.AddComponent<Rigidbody>();
                // finalMassObject.GetComponent<Rigidbody>().drag = 1f;
                // finalMassObject.GetComponent<Rigidbody>().useGravity = false;
                // finalMassObject.GetComponent<Rigidbody>().isKinematic = true;
                // finalMassObject.GetComponent<CapsuleCollider>().isTrigger = true;
                finalMassObject.GetComponent<SphereCollider>().isTrigger = true;
                finalMassObject.GetComponent<Renderer>().material.SetColor("_Color",Color.red);
                // finalMassObject.GetComponent<Renderer>().material.enableInstancing = true;
                finalMassObject.transform.SetParent(this.transform);
                massList[i] = finalMassObject;
            }
            else{
                // springMassObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                springMassObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                springMassObject.name = "Mass" + i.ToString();
                springMassObject.transform.localScale = 2f*scaleVector;
                // springMassObject.AddComponent<Rigidbody>();
                // springMassObject.GetComponent<Rigidbody>().drag = 1f;
                // springMassObject.GetComponent<Rigidbody>().useGravity = false;
                // springMassObject.GetComponent<Rigidbody>().isKinematic = true;
                
                // springMassObject.GetComponent<CapsuleCollider>().isTrigger = true;
                springMassObject.GetComponent<SphereCollider>().isTrigger = true;
                springMassObject.GetComponent<Renderer>().material.SetColor("_Color",Color.red);
                // springMassObject.GetComponent<Renderer>().material.enableInstancing = true;
                springMassObject.transform.SetParent(this.transform);
                massList[i] = springMassObject;

            }

            //At this point all the masses are at the same location
            varPos[0] = origin;

            if (i>0){
                totalLength += (currentDefinePos - prevDefinePos).magnitude;
                currentDir = endPos[i] - endPos[i-1];
                float segmentLength = currentDir.magnitude;
                cochlearLength += segmentLength;
                Vector3 path = new Vector3(0,i*segmentLength,0);
                varPos[i] = origin + path;

                currentToPrev = Quaternion.FromToRotation(currentDir,prevDir);
                endRotations[i-1] = currentToPrev;
                prevDir = currentDir; 
            }

            prevDefinePos = currentDefinePos;

        }
            

        for (int i=0;i<numPoints;i++){

            if (evenDistribution){
                Vector3 point = new Vector3(0,(float)i*cochlearLength/numPoints,0);
                massList[i].transform.localPosition = origin + point;
                if (i>0){
                    float distance = Vector3.Distance(massList[i].transform.localPosition,massList[i-1].transform.localPosition);
                }
            }

            else{
                massList[i].transform.localPosition = varPos[i];
                if (i>0){
                    float distance = Vector3.Distance(massList[i].transform.localPosition,massList[i-1].transform.localPosition);
                }
            }

        }

        // for (int i=1; i<numPoints;i++){
        //     massList[i].AddComponent<ConfigurableJoint>();
        //     // massList[i].GetComponent<ConfigurableJoint>().configuredInWorldSpace = true;
        //     massList[i].GetComponent<ConfigurableJoint>().enablePreprocessing = true;
        //     massList[i].GetComponent<ConfigurableJoint>().connectedBody = massList[i-1].GetComponent<Rigidbody>();
        //     // massList[i].GetComponent<ConfigurableJoint>().xMotion = ConfigurableJointMotion.Locked;
        //     // massList[i].GetComponent<ConfigurableJoint>().yMotion = ConfigurableJointMotion.Locked;
        //     massList[i].GetComponent<ConfigurableJoint>().zMotion = ConfigurableJointMotion.Locked;
        //     massList[i].GetComponent<ConfigurableJoint>().angularXMotion = ConfigurableJointMotion.Locked;
        //     massList[i].GetComponent<ConfigurableJoint>().angularYMotion = ConfigurableJointMotion.Locked;
        //     // massList[i].GetComponent<ConfigurableJoint>().angularZMotion = ConfigurableJointMotion.Limited;
        //     massList[i].GetComponent<ConfigurableJoint>().enableCollision = true;
        //     massList[i].GetComponent<ConfigurableJoint>().autoConfigureConnectedAnchor = false;
        //     massList[i].GetComponent<ConfigurableJoint>().connectedAnchor = new Vector3(0f,1.0f,0f);

                     
        // //     // massList[i].AddComponent<FixedJoint>();
        // //     // massList[i].GetComponent<FixedJoint>().connectedBody = massList[i-1].GetComponent<Rigidbody>();
        // //     // massList[i].GetComponent<FixedJoint>().enableCollision = true;

        
        //     SoftJointLimitSpring springJointLimit = new SoftJointLimitSpring();
        //     springJointLimit.damper = 10f;
        //     springJointLimit.spring = 6f;
        //     SoftJointLimit jointLimit = new SoftJointLimit();
        //     jointLimit.limit=2.5f;
        //     massList[i].GetComponent<ConfigurableJoint>().linearLimit = jointLimit;
        //     massList[i].GetComponent<ConfigurableJoint>().linearLimitSpring = springJointLimit;
        // }
        
        


        // print(cochlearLength);
        // print(totalLength);

         
    }

    //When values in inspector changed
    void OnValidate(){
        if (finalMassObject){
            this.transform.RotateAround(finalMassObject.transform.position,Vector3.forward,angleOfInsertion - previousAngleOfInsertion);
            previousAngleOfInsertion = angleOfInsertion;
        }
        
        CallUpdate();
    }

    void Start(){
        
        CallUpdate();
    }

    // bool ResetReady(){
        

    //     if (Mathf.Abs(finalMassObject.transform.position.y - finalMassPos) > 0.001f){
    //         finalMassPos = finalMassObject.transform.position.y;
    //         return false;
    //     } 
    //     else{
    //         return true;
    //     }
    // }

    void FixedUpdate(){
        if (manualInsertion){
            int increaseInsertionVel = Convert.ToInt32( Input.GetKey("w") );
            int decreaseIInsertionVel = Convert.ToInt32( Input.GetKey("s") );
            int increaseCurlingMod = Convert.ToInt32( Input.GetKey("d") );
            int decreaseCurlingMod = Convert.ToInt32( Input.GetKey("a") );

            electrodeInsertionSpeed = 0.5f*increaseInsertionVel - 0.5f*decreaseIInsertionVel;
            // interp = interp-Time.fixedDeltaTime/timeToCurl;

            curlingModifier = Mathf.Clamp( curlingModifier + 0.005f*increaseCurlingMod - 0.005f*decreaseCurlingMod, 0f, 2.5f);
        }
        if (automateInsertionWithSpatial){
            //Change the insertionSpeed based on distance form wall in 2D plane while keeping the curling rate fixed
            //If detect hit with wall, start again and change the parameters and compare distance travelled
            interp = Mathf.Clamp(interp-Time.fixedDeltaTime/timeToCurl,0f,1f);
            // ShortestDistance();
            if (distInner < scale || distOuter< scale){
                //reset everything
                UnityEngine.Debug.Log("Reset");
                if (interp < bestInterp){
                    //This run is better than a previous run so save the scalar
                
                    bestInterp = interp;
                    prevWallScalar = wallScalar;
                    wallScalar += UnityEngine.Random.Range(0f,1f);
                    UnityEngine.Debug.Log("Better: " + wallScalar.ToString());
                }
                else{
                    //This run is worse than the previous run
                    wallScalar = prevWallScalar += UnityEngine.Random.Range(0f,3f);
                }

                this.transform.position = startingPosition;
                interp = 1.0f;
                electrodeInsertionSpeed = 0;
                automateInsertionWithSpatial = false;
            }
            else if (distInner > 0.5f && distInner < 2.0f){
                electrodeInsertionSpeed = wallScalar*cochlearLength/(timeToCurl/Time.fixedDeltaTime);
            }
            else if (distOuter > 0.5f && distOuter<2.0f){
                electrodeInsertionSpeed = 1/wallScalar*cochlearLength/(timeToCurl/Time.fixedDeltaTime);
            }
        
            else{
                electrodeInsertionSpeed = cochlearLength/(timeToCurl/Time.fixedDeltaTime);
            }

            if (interp < 0.00001f){
                //We made it!!
                UnityEngine.Debug.Log("We made it: " + wallScalar.ToString());
                electrodeInsertionSpeed = 0;
                automateInsertionWithSpatial = false;
            }

        }

        if (automateInsertion){
            automateRetraction = false;
            electrodeInsertionSpeed = cochlearLength/timeToCurl;
            // electrodeInsertionSpeed = cochlearLength/(timeToCurl/Time.fixedDeltaTime);
            // interp = Mathf.Clamp(interp-Time.fixedDeltaTime/timeToCurl,0f,1f);
            previousInterp = interp;
            interp = interp-Time.fixedDeltaTime/timeToCurl;

            linearPosOutput.Add(this.transform.position.y);
            linearVelOutput.Add(this.transform.GetComponent<Rigidbody>().velocity.y);
            rotaryPosOutput.Add(electrodeRoll);
            // timeOutput.Add(myTimer.ElapsedMilliseconds);

            if (interp < 0.00001f){
                electrodeInsertionSpeed = 0;
                automateInsertion = false;
            }
        }

        if (automateNonlinearInsertion){
            //To work properly with velocity, I need to be moving the joints instead but this is not configured correctly
            //Let's just take the previous - current/Time, which will have arbitary units



            numSteps+=1;
            automateInsertion = false;
            interp = Mathf.Clamp(interp - Time.fixedDeltaTime/timeToCurl,0f,1f);
            //Make electrode Speed follow a vector? 
            electrodeInsertionSpeed = 0.1f*(Mathf.Sin(Time.fixedDeltaTime*numSteps)+1);
            //Total distance will just be integral of above so -0.1cos(T) + T + 0.1
            linearPosOutput.Add(electrodeInsertionSpeed);
            rotaryPosOutput.Add(electrodeRoll);
            timeOutput.Add(numSteps*Time.fixedDeltaTime);

            if (interp < 0.00001f){
                UnityEngine.Debug.Log( (numSteps*Time.fixedDeltaTime).ToString() );
                UnityEngine.Debug.Log( (-0.1f*Mathf.Cos(numSteps*Time.fixedDeltaTime) + numSteps*Time.fixedDeltaTime + 0.1f).ToString());
                electrodeInsertionSpeed = 0;
                automateNonlinearInsertion = false;
            }
        }


        if (automateRetraction){
            automateInsertion = false;
            electrodeInsertionSpeed = -cochlearLength/(timeToCurl/Time.fixedDeltaTime);
            interp = Mathf.Clamp(interp+Time.fixedDeltaTime/timeToCurl,0f,1f);
            if (interp >= 0.9999f){
                electrodeInsertionSpeed = 0;
                automateRetraction = false;
            }
        }

        // qAngle = Quaternion.AngleAxis(-electrodeRoll,Vector3.up);
        qAngle = Quaternion.AngleAxis(-electrodeRoll,massList[0].transform.up);
        insertionAngle = Quaternion.AngleAxis(angleOfInsertion,this.transform.forward);
        
        
        //Since fixedDeltaTime is 0.02seconds this will move 0.02*speed units per frame. Why does it stop if interp forces are running?
        //This still bounces up and down
        // this.transform.Translate(this.transform.up*electrodeInsertionSpeed*Time.fixedDeltaTime);
        this.GetComponent<Rigidbody>().AddForce(Vector3.up * electrodeInsertionSpeed);

        //Apply the speed as a force just to check
        // foreach (GameObject mass in massList){
            // mass.transform.GetComponent<Rigidbody>().MovePosition(mass.transform.position + Vector3.up*electrodeInsertionSpeed*Time.fixedDeltaTime);
            // mass.transform.GetComponent<Rigidbody>().velocity += Vector3.up*electrodeInsertionSpeed*Time.deltaTime;
            // mass.transform.GetComponent<ConfigurableJoint>().targetVelocity.y 
            // mass.transform.GetComponent<Rigidbody>().AddForce(Vector3.up*electrodeInsertionSpeed,ForceMode.Force);
        // }
        
        // this.transform.Translate(Vector3.up*electrodeInsertionSpeed);
        
        // finalMassObject.transform.GetComponent<Rigidbody>().MovePosition(finalMassObject.transform.position + velocityVec.normalized * electrodeInsertionSpeed * Time.fixedDeltaTime);

        if (getData){
            print(finalMassObject.transform.position);
            getData = false;
        }

        CallUpdate();
            

        // ShortestDistance();
    }

    void CallUpdate(){
        if (mesh != null && Application.isPlaying){
            MoveCochlearVertices();
            UpdateMesh();
        }
    }

    void ShortestDistance(){
        // Vector3 start = transform.position+(vertices[total-1]+vertices[total-1-nGon/2])/2;
        Vector3 start = finalMassObject.transform.position;

        RaycastHit hitSphereInner;
        RaycastHit hitSphereOuter;
        //Actually it is not this, we need to follow the direction of the tip. Will change this
        Vector3 dirInner = finalMassObject.transform.right;
        Vector3 dirOuter = -dirInner;

        if (Physics.SphereCast(start,0.001f,dirInner,out hitSphereInner,100,layerMask)){
            distInner = hitSphereInner.distance;
            // UnityEngine.Debug.Log("Distance to Inner wall: " + distInner.ToString());
            UnityEngine.Debug.DrawRay(start,dirInner*distInner,Color.red);
        }
        if (Physics.SphereCast(start,0.001f,dirOuter,out hitSphereOuter,100,layerMask)){
            distOuter = hitSphereOuter.distance;
            // UnityEngine.Debug.Log("Distance to Outer wall: " + distOuter.ToString());
            UnityEngine.Debug.DrawRay(start,dirOuter*distOuter,Color.yellow);
        }
        
        // RaycastHit hitSphere;
        // for (int i=0;i<360;i++){
        //     Vector3 dic = Quaternion.Euler(0,i,0)*Vector3.right;
        //     if (Physics.SphereCast(start,0.001f,dic,out hitSphere,100,layerMask)){
        //         if (hitSphere.distance<dist){
        //             dist = hitSphere.distance;
        //             hitDirection = dic;
        //         }
        //     }
        // }
        // UnityEngine.Debug.DrawRay(start,hitDirection*dist,Color.red);
        // if(dist<scale/2){
        //     interp = Mathf.Clamp(interp-Time.fixedDeltaTime/timeToCurl,0f,1f);

        // }
        // dist = Mathf.Infinity;
    }


    void MoveCochlearVertices(){
        int v=0;
        startRot = new Quaternion(0,0,0,1);
        varRot = startRot;
        prevCurlingPoint = Vector3.zero;
        for (int i=0;i<numPoints;i++){
            if (i>0){
                if (i>criticalPoint){
                    //Why does this all get fucked up if I do the automatic insertion procedure?? **** Must fix this
                    float v1 = i;
                    float mod = curlingModifier * v1 / (numPoints - 1);
                    modifiedInterp = Mathf.Clamp(interp*(1-mod),0,1); // Only for curling?
                    varRotations[i-1] = Quaternion.Slerp(startRot,endRotations[i-1],modifiedInterp);
                }
                else {
                    varRotations[i-1] = Quaternion.Slerp(startRot,endRotations[i-1],interp);
                }

                varRot *= varRotations[i-1];

                currentCurlingPoint = endPos[i];
                pivot = endPos[i-1];

                currentCurlingPoint -= pivot;
                currentCurlingPoint = varRot*currentCurlingPoint;
                currentCurlingPoint += prevCurlingPoint;

                varPos[i] = currentCurlingPoint;
                
            }
            
            basePoint = varPos[i];
            
            
            Vector3 rotatedBasePoint = qAngle * basePoint;
            if (i==0){
                forceDir = (basePoint - massList[i].transform.position);
            }

            else{
                forceDir = (rotatedBasePoint - massList[i].transform.position);
            }
        
            // UnityEngine.Debug.DrawRay(massList[i].transform.position,forceDir,Color.green,100);
            // Only Want this force if interp is changing

            // massList[i].GetComponent<Rigidbody>().MovePosition(rotatedBasePoint);
            // massList[i].GetComponent<Rigidbody>().MoveRotation(varRot);
            // if (interp>0.0001f && interp<0.9999f){
            // massList[i].GetComponent<Rigidbody>().AddForce(forceDir);
            // massList[i].transform.GetComponent<Rigidbody>().velocity += forceDir*Time.deltaTime;
            if (i==0){
                massList[i].transform.localPosition = basePoint;
            }
            else{

                massList[i].transform.localPosition = basePoint;
                massList[i].transform.RotateAround(massList[0].transform.position,Vector3.up,electrodeRoll);
                // massList[i].transform.localRotation = varRot;
                
            }
            

            // massList[i].GetComponent<Rigidbody>().velocity = forceDir;
            planeNormal = (basePoint-prevCurlingPoint);
            rotatePolygonPlane = Vector3.SignedAngle(Vector3.up,planeNormal,Vector3.forward);
            for (int n=0;n<nGon;n++){
                nGonPoint = new Vector3(scale*Mathf.Cos(2*Mathf.PI*n/nGon),0,scale*Mathf.Sin(2*Mathf.PI*n/nGon));
                nGonPoint = Quaternion.AngleAxis(rotatePolygonPlane,Vector3.forward)*nGonPoint;

                vertices[v] = massList[i].transform.localPosition + qAngle*nGonPoint;
                v+=1;
            }
            prevCurlingPoint = basePoint;
        }
        previousInterp = interp;

        DefineTriangles();
    }

    void DefineTriangles(){
        int v=0;
        int t=0;
        for (int i=0;i<numPoints-1;i++){
            for (int n=0;n<nGon;n++){

                triangles[t] = v;
                triangles[t+1] = triangles[t+4] = (v+1)%(nGon)+nGon*i;
                triangles[t+2] = triangles[t+3] = (v+nGon);
                triangles[t+5] = (v+1)%(nGon) + nGon*(1+i);

                triangles[t+oneSideTri] = (v+1)%(nGon)+nGon*i;
                triangles[t+1+oneSideTri] = triangles[t+4+oneSideTri] = v;
                triangles[t+2+oneSideTri] = triangles[t+3+oneSideTri] = (v+1)%(nGon) + nGon*(1+i);
                triangles[t+5+oneSideTri] = (v+nGon);


                v+=1;
                t+=6;
            }
        }

    }

    private void OnTriggerEnter(Collider other)
    {
        UnityEngine.Debug.Log("Collide Event");
    }
    // // void MeshCollision() {
    // //     // check collisions
    // //     for (int i=0;i<numPoints;i++){
    // //         hitColliders = Physics.OverlapCapsule()
    // //     }
    // //     hitColliders = Physics.OverlapSphere(this.transform.position,0.5f,layerMask);
    // //     // int numOverlaps = Physics.OverlapSphereNonAlloc(this.transform.position,this.GetComponent<SphereCollider>().radius,hitColliders,layerMask,QueryTriggerInteraction.UseGlobal);
    // //     // if (numOverlaps>0){
    // //     //     Debug.Log("Here: " + numOverlaps.ToString());
    // //     // }
        
    // //     // // int numOverlaps = Physics.OverlapBoxNonAlloc(this.transform.position,this.transform.localScale*-0.5f,hitColliders,this.transform.rotation,layerMask,QueryTriggerInteraction.UseGlobal);
    // //     for (int i = 0; i < hitColliders.Length; i++) {
    // //         var collider = hitColliders[i];
    // //         Vector3 otherPosition = collider.gameObject.transform.position;
    // //         Quaternion otherRotation = collider.gameObject.transform.rotation;
    // //         Vector3 direction;
    // //         float distance;

    // //         bool overlap = Physics.ComputePenetration(this.GetComponent<SphereCollider>(),this.transform.position,
    // //             this.transform.rotation,collider,otherPosition,
    // //             otherRotation,out direction,out distance);

    // //         if (overlap){
    // //             penetrationForce = -10*CollisionForce*(direction*distance);
                
    // //             // Vector3 movementDirection = moveDir + penetrationVector;
    // //             // Vector3 velocityProjected = Vector3.Project(rb.velocity, -direction);
    // //             rb.AddRelativeForce(penetrationForce);

    // //             // moveDir = movementDirection.normalized;
    // //             // this.transform.position = this.transform.position + penetrationVector;
    // //             // velocity -= velocityProjected;
    // //             Debug.Log("OnCollisionEnter with " + hitColliders[i].gameObject.name + " penetration vector: " + penetrationForce.ToString("F3"));
    // //         }
    // //         else
    // //         {
    // //             Debug.Log("OnCollision Enter with " + hitColliders[i].gameObject.name + " no penetration");
    // //         }
    // //     }

    // // }

    // void OnDrawGizmos()
    // {
    //     Gizmos.color = Color.blue;
    //     for (int i=1;i<numPoints;i++){
    //         Gizmos.DrawLine(endPos[i], varPos[i]);
    //     }
    // }

    void UpdateMesh(){
        mesh.Clear(); //Clear out the current buffer 
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        // mesh.RecalculateNormals(); //Calculate normal based on triangle vertices are part of
    }
}
