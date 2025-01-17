﻿/* entity-relations.module.js */

/**
* @desc this module manages the entity relations screen in the administration
*/

(function () {
    'use strict';

    angular
        .module('webvellaAdmin') //only gets the module, already initialized in the base.module of the plugin. The lack of dependency [] makes the difference.
        .config(config)
        .controller('WebVellaAdminEntityRelationsController', controller)
        .controller('ManageRelationModalController', ManageRelationModalController)
        .controller('DeleteRelationModalController', DeleteRelationModalController);
    
    // Configuration ///////////////////////////////////
    config.$inject = ['$stateProvider'];

    /* @ngInject */
    function config($stateProvider) {
        $stateProvider.state('webvella-admin-entity-relations', {
            parent: 'webvella-admin-base',
            url: '/entities/:entityName/relations', //  /desktop/areas after the parent state is prepended
            views: {
                "topnavView": {
                    controller: 'WebVellaAdminTopnavController',
                    templateUrl: '/plugins/webvella-admin/topnav.view.html',
                    controllerAs: 'topnavData'
                },
                "sidebarView": {
                    controller: 'WebVellaAdminSidebarController',
                    templateUrl: '/plugins/webvella-admin/sidebar.view.html',
                    controllerAs: 'sidebarData'
                },
                "contentView": {
                    controller: 'WebVellaAdminEntityRelationsController',
                    templateUrl: '/plugins/webvella-admin/entity-relations.view.html',
                    controllerAs: 'contentData'
                }
            },
            resolve: {
            	checkedAccessPermission: checkAccessPermission,
                resolvedRelationsList: resolveRelationsList,
                resolvedCurrentEntityMeta: resolveCurrentEntityMeta,
                resolvedEntityList:resolveEntityList
            },
            data: {

            }
        });
    };


	// Resolve Function /////////////////////////
    checkAccessPermission.$inject = ['$q', '$log', 'resolvedCurrentUser', 'ngToast'];
	/* @ngInject */
    function checkAccessPermission($q, $log, resolvedCurrentUser, ngToast) {
    	$log.debug('webvellaAreas>entities> BEGIN check access permission ' + moment().format('HH:mm:ss SSSS'));
    	var defer = $q.defer();
    	var messageContent = '<span class="go-red">No access:</span> You do not have access to the <span class="go-red">Admin</span> area';
    	var accessPermission = false;
    	for (var i = 0; i < resolvedCurrentUser.roles.length; i++) {
    		if (resolvedCurrentUser.roles[i] == "bdc56420-caf0-4030-8a0e-d264938e0cda") {
    			accessPermission = true;
    		}
    	}

    	if (accessPermission) {
    		defer.resolve();
    	}
    	else {

    		ngToast.create({
    			className: 'error',
    			content: messageContent
    		});
    		defer.reject("No access");
    	}

    	$log.debug('webvellaAreas>entities> BEGIN check access permission ' + moment().format('HH:mm:ss SSSS'));
    	return defer.promise;
    }

    resolveRelationsList.$inject = ['$q', '$log', 'webvellaAdminService', '$state', '$timeout'];
    /* @ngInject */
    function resolveRelationsList($q, $log, webvellaAdminService, $state, $timeout) {
    	$log.debug('webvellaAdmin>entity-relations> BEGIN resolveRelationsList state.resolved ' + moment().format('HH:mm:ss SSSS'));
        // Initialize
        var defer = $q.defer();

        // Process
        function successCallback(response) {
            defer.resolve(response.object);
        }

        function errorCallback(response) {
        	defer.reject(response.message);
        }

        webvellaAdminService.getRelationsList(successCallback, errorCallback);

        // Return
        $log.debug('webvellaAdmin>entity-relations> END resolveRelationsList state.resolved ' + moment().format('HH:mm:ss SSSS'));
        return defer.promise;
    }

    resolveCurrentEntityMeta.$inject = ['$q', '$log', 'webvellaAdminService', '$stateParams', '$state', '$timeout'];
    /* @ngInject */
    function resolveCurrentEntityMeta($q, $log, webvellaAdminService, $stateParams, $state, $timeout) {
    	$log.debug('webvellaAdmin>entity-details> BEGIN resolveCurrentEntityMeta state.resolved ' + moment().format('HH:mm:ss SSSS'));
        // Initialize
        var defer = $q.defer();

        // Process
        function successCallback(response) {
            if (response.object == null) {
                $timeout(function () {
                    $state.go("webvella-root-not-found");
                }, 0);
            }
            else {
                defer.resolve(response.object);
            }
        }

        function errorCallback(response) {
            if (response.object == null) {
                $timeout(function () {
                    $state.go("webvella-root-not-found");
                }, 0);
            }
            else {
            	defer.reject(response.message);
            }
        }

        webvellaAdminService.getEntityMeta($stateParams.entityName, successCallback, errorCallback);

        // Return
        $log.debug('webvellaAdmin>entity-details> END resolveCurrentEntityMeta state.resolved ' + moment().format('HH:mm:ss SSSS'));
        return defer.promise;
    }

    resolveEntityList.$inject = ['$q', '$log', 'webvellaAdminService', '$state', '$timeout'];
    /* @ngInject */
    function resolveEntityList($q, $log, webvellaAdminService, $state, $timeout) {
    	$log.debug('webvellaAdmin>entity-relations> BEGIN resolveEntityList state.resolved ' + moment().format('HH:mm:ss SSSS'));
        // Initialize
        var defer = $q.defer();

        // Process
        function successCallback(response) {
            defer.resolve(response.object);
        }

        function errorCallback(response) {
        	defer.reject(response.message);
        }

        webvellaAdminService.getMetaEntityList(successCallback, errorCallback);

        // Return
        $log.debug('webvellaAdmin>entity-relations> END resolveEntityList state.resolved ' + moment().format('HH:mm:ss SSSS'));
        return defer.promise;
    }


    // Controller ///////////////////////////////
    controller.$inject = ['$scope', '$log', '$rootScope', '$state', 'pageTitle', 'resolvedRelationsList', 'resolvedCurrentEntityMeta', 'resolvedEntityList', '$modal'];

    /* @ngInject */
    function controller($scope, $log, $rootScope, $state, pageTitle, resolvedRelationsList,resolvedCurrentEntityMeta,resolvedEntityList, $modal) {
    	$log.debug('webvellaAdmin>entity-relations> START controller.exec ' + moment().format('HH:mm:ss SSSS'));
        /* jshint validthis:true */
        var contentData = this;
        contentData.search = {};
        contentData.allRelations = angular.copy(resolvedRelationsList);
        contentData.currentEntityRelation = [];
        contentData.entity = angular.copy(resolvedCurrentEntityMeta);
        contentData.entityList = angular.copy(resolvedEntityList.entities);

        //Initialize relations in the scope of this entity
        for (var i = 0; i < contentData.allRelations.length; i++) {
            if (contentData.allRelations[i].originEntityId == contentData.entity.id || contentData.allRelations[i].targetEntityId == contentData.entity.id) {
                contentData.currentEntityRelation.push(contentData.allRelations[i]);
            }
        }
        for (var j = 0; j < contentData.currentEntityRelation.length; j++) {
            for (var k = 0; k < contentData.entityList.length; k++) {
                if (contentData.currentEntityRelation[j].originEntityId == contentData.entityList[k].id) {
                    //add origin Name 
                    contentData.currentEntityRelation[j].originEntityName = contentData.entityList[k].name;
                    //add origin label 
                    contentData.currentEntityRelation[j].originEntityLabel = contentData.entityList[k].label;

                    for (var m = 0; m < contentData.entityList[k].fields.length; m++) {
                        if (contentData.entityList[k].fields[m].id == contentData.currentEntityRelation[j].originFieldId) {
                            //add target Name 
                            contentData.currentEntityRelation[j].originFieldName = contentData.entityList[k].fields[m].name;
                            //add target Label 
                            contentData.currentEntityRelation[j].originFieldLabel = contentData.entityList[k].fields[m].label;
                        }
                    }
                }
                if (contentData.currentEntityRelation[j].targetEntityId == contentData.entityList[k].id) {
                    //add target Name 
                    contentData.currentEntityRelation[j].targetEntityName = contentData.entityList[k].name;
                    //add target Label 
                    contentData.currentEntityRelation[j].targetEntityLabel = contentData.entityList[k].label

                    for (var m = 0; m < contentData.entityList[k].fields.length; m++) {
                        if (contentData.entityList[k].fields[m].id == contentData.currentEntityRelation[j].targetFieldId) {
                            //add target Name 
                            contentData.currentEntityRelation[j].targetFieldName = contentData.entityList[k].fields[m].name;
                            //add target Label 
                            contentData.currentEntityRelation[j].targetFieldLabel = contentData.entityList[k].fields[m].label;
                        }
                    }
                }
            }
        }

        //Update page title
        contentData.pageTitle = "Entity Relations | " + pageTitle;
        $rootScope.$emit("application-pageTitle-update", contentData.pageTitle);
        //Hide Sidemenu
        $rootScope.$emit("application-body-sidebar-menu-isVisible-update", false);
        $log.debug('rootScope>events> "application-body-sidebar-menu-isVisible-update" emitted ' + moment().format('HH:mm:ss SSSS'));
        $scope.$on("$destroy", function () {
            $rootScope.$emit("application-body-sidebar-menu-isVisible-update", true);
            $log.debug('rootScope>events> "application-body-sidebar-menu-isVisible-update" emitted ' + moment().format('HH:mm:ss SSSS'));
        });


        contentData.manageRelationModal = function (relation) {
            var modalInstance = $modal.open({
                animation: false,
                templateUrl: 'manageRelationModal.html',
                controller: 'ManageRelationModalController',
                controllerAs: "popupData",
                size: "lg",
                resolve: {
                    contentData: function () {
                        return contentData;
                    },
                    managedRelation: function () {
                        var relationObject = null;
                        if (relation != null) {
                            for (var i = 0; i < resolvedRelationsList.length; i++) {
                                if (relation.id === resolvedRelationsList[i].id) {
                                    relationObject = resolvedRelationsList[i];
                                }
                            }
                        }
                        return relationObject;
                    }
                }
            });
        }
    

        activate();
        $log.debug('webvellaAdmin>entity-relations> END controller.exec ' + moment().format('HH:mm:ss SSSS'));
        function activate() { }
    }


    //// Modal Controllers
    ManageRelationModalController.$inject = ['$modalInstance','$modal', '$log', 'webvellaAdminService', 'webvellaRootService', 'ngToast', '$timeout', '$state', '$location', 'contentData', 'managedRelation'];

    /* @ngInject */
    function ManageRelationModalController($modalInstance,$modal, $log, webvellaAdminService, webvellaRootService, ngToast, $timeout, $state, $location, contentData, managedRelation) {
    	$log.debug('webvellaAdmin>entities>CreateRelationModalController> START controller.exec ' + moment().format('HH:mm:ss SSSS'));
        /* jshint validthis:true */
        var popupData = this;
        popupData.modalInstance = $modalInstance;
        if (managedRelation === null) {
            popupData.relation = webvellaAdminService.initRelation();
        }
        else {
            popupData.relation = angular.copy(managedRelation);
        }

        popupData.currentEntity = contentData.entity;

        //Relation type array
        popupData.relationTypeDict = [
            {
                name: "One to One (1:1)",
                key: 1

            },
            {
                name: "One to Many (1:N)",
                key: 2

            },
            {
                name: "Many to Many (N:N)",
                key: 3

            }
        ];

        popupData.selectedOriginEntity = {};
        popupData.selectedOriginEntity.fields = [{
            id: 1,
            name: "Select Origin Entity first",
            label:"Select Origin Entity first"
        }];
        popupData.selectedOriginFieldEnabled = false;
        popupData.selectedOriginField = popupData.selectedOriginEntity.fields[0];
        popupData.selectedTargetEntity = {};
        popupData.selectedTargetEntity.fields = [{
            id: 1,
            name: "Select Target Entity first",
            label: "Select Target Entity first"
        }];
        popupData.selectedTargetFieldEnabled = false;
        popupData.selectedTargetField = popupData.selectedTargetEntity.fields[0];
        popupData.entities = contentData.entityList;
        //Generate the eligible ORIGIN entities list. No limitation for the eligible field to have only one relation as Target;
        popupData.eligibleOriginEntities = [];
        for (var i = 0; i < popupData.entities.length; i++) {
            var entity = {};
            entity.id = popupData.entities[i].id;
            entity.name = popupData.entities[i].name;
            entity.label = popupData.entities[i].label;
            entity.enabled = true;
            entity.fields = [];
            for (var j = 0; j < popupData.entities[i].fields.length; j++) {
                if (popupData.entities[i].fields[j].fieldType === 16 && popupData.entities[i].fields[j].required && popupData.entities[i].fields[j].unique) {
                    var field = {};
                    field.id = popupData.entities[i].fields[j].id;
                    field.name = popupData.entities[i].fields[j].name;
                    field.label = popupData.entities[i].fields[j].label;
                    field.required = popupData.entities[i].fields[j].required;
                    field.unique = popupData.entities[i].fields[j].unique;
                    entity.fields.push(field);
                }
            }
            //Add entity only if it has more than one field 
            if (entity.fields.length > 0) {
                popupData.eligibleOriginEntities.push(entity);
            }
        }
        popupData.eligibleOriginEntities = popupData.eligibleOriginEntities.sort(function (a, b) {
            if (a.name < b.name) return -1;
            if (a.name > b.name) return 1;
            return 0;
        });


        //Generate the eligible Target entities list. Enforced limitation for the eligible field to have only one relation as Target;
        //Generate an array of field Ids that are targets in relations
        popupData.targetedFields = [];
        for (var i = 0; i < contentData.allRelations.length; i++) {
            popupData.targetedFields.push(contentData.allRelations[i].targetFieldId);
        }

        popupData.eligibleTargetEntities = [];
        for (var i = 0; i < popupData.entities.length; i++) {
            var entity = {};
            entity.id = popupData.entities[i].id;
            entity.name = popupData.entities[i].name;
            entity.label = popupData.entities[i].label;
            entity.enabled = true;
            entity.fields = [];
            for (var j = 0; j < popupData.entities[i].fields.length; j++) {
                if (popupData.entities[i].fields[j].fieldType === 16) {
                    var field = {};
                    //Add the field only if it is not already a target for a relation
                    if (popupData.targetedFields.indexOf(popupData.entities[i].fields[j].id) === -1) {
                        field.id = popupData.entities[i].fields[j].id;
                        field.name = popupData.entities[i].fields[j].name;
                        field.label = popupData.entities[i].fields[j].label;
                        field.required = popupData.entities[i].fields[j].required;
                        field.unique = popupData.entities[i].fields[j].unique;
                        entity.fields.push(field);
                    }
                }
            }
            //Add entity only if it has more than one field 
            if (entity.fields.length > 0) {
                popupData.eligibleTargetEntities.push(entity);
            }
        }

        popupData.eligibleTargetEntities = popupData.eligibleTargetEntities.sort(function (a, b) {
            if (a.name < b.name) return -1;
            if (a.name > b.name) return 1;
            return 0;
        });

        ///////
        popupData.changeOriginEntity = function (newEntityModel) {
            //get only fields that are required
            var requiredFields = [];
            for (var i = 0; i < newEntityModel.fields.length; i++) {
                if (newEntityModel.fields[i].required && newEntityModel.fields[i].unique) {
                    requiredFields.push(newEntityModel.fields[i]);
                }
            }
            newEntityModel.fields = requiredFields;
            newEntityModel.fields = newEntityModel.fields.sort(function (a, b) {
                if (a.name < b.name) return -1;
                if (a.name > b.name) return 1;
                return 0;
            });
            popupData.selectedOriginField = newEntityModel.fields[0];
            popupData.selectedOriginFieldEnabled = true;

            if ((popupData.relation.relationType != 3) && popupData.selectedOriginField && popupData.selectedOriginField.id === popupData.selectedTargetField.id) {
                popupData.fieldsDuplicatedError = true;
            } else {
                popupData.fieldsDuplicatedError = false;
            }
        }

        //////
        popupData.changeTargetEntity = function (newEntityModel) {
            newEntityModel.fields = newEntityModel.fields.sort(function (a, b) {
                if (a.name < b.name) return -1;
                if (a.name > b.name) return 1;
                return 0;
            });

            popupData.selectedTargetField = newEntityModel.fields[0];
            if (newEntityModel.fields[0].name == "id" && (popupData.relation.relationType == 1 || popupData.relation.relationType == 2)) {
                if (newEntityModel.fields.length > 1) { // if there are more fields than just the id
                    popupData.selectedTargetField = newEntityModel.fields[1];
                }
                else {
                    popupData.selectedTargetField = null;
                }
            }

            popupData.selectedTargetFieldEnabled = true;
            if ((popupData.relation.relationType != 3) && popupData.selectedOriginField.id == popupData.selectedTargetField.id) {
                popupData.fieldsDuplicatedError = true;
            } else {
                popupData.fieldsDuplicatedError = false;
            }
        }

        //Validation for duplicated fields
        popupData.fieldsDuplicatedError = false;

        popupData.changeField = function () {
        	if ((popupData.relation.relationType != 3) && popupData.selectedOriginField.id == popupData.selectedTargetField.id) {
                popupData.fieldsDuplicatedError = true;
            } else {
                popupData.fieldsDuplicatedError = false;
            }
        }


        //Initialize the selectedEntity and Fields if in manage mode
        if (popupData.relation.id != null) {
            popupData.selectedTargetEntityLabel = "";
            popupData.selectedOriginEntityLabel = "";
            popupData.selectedTargetFieldLabel = "";
            popupData.selectedOriginFieldLabel = "";
            for (var i = 0; i < popupData.entities.length; i++) {
                //initialize target
                if (popupData.entities[i].id === popupData.relation.targetEntityId) {
                    popupData.selectedTargetEntityLabel = popupData.entities[i].label;
                    for (var j = 0; j < popupData.entities[i].fields.length; j++) {
                        if (popupData.entities[i].fields[j].id === popupData.relation.targetFieldId) {
                            popupData.selectedTargetFieldLabel = popupData.entities[i].fields[j].label;
                        }
                    }
                }
                // initialize origin
                if (popupData.entities[i].id === popupData.relation.originEntityId) {
                    popupData.selectedOriginEntityLabel = popupData.entities[i].label;
                    for (var j = 0; j < popupData.entities[i].fields.length; j++) {
                        if (popupData.entities[i].fields[j].id === popupData.relation.originFieldId) {
                            popupData.selectedOriginFieldLabel = popupData.entities[i].fields[j].label;
                        }
                    }
                }
            }
        }


        //////
        popupData.ok = function () {
            popupData.relation.originEntityId = popupData.selectedOriginEntity.id;
            popupData.relation.originFieldId = popupData.selectedOriginField.id;
            popupData.relation.targetEntityId = popupData.selectedTargetEntity.id;
            popupData.relation.targetFieldId = popupData.selectedTargetField.id;
            if (popupData.relation.id === null) {
                webvellaAdminService.createRelation(popupData.relation, successCallback, errorCallback)
            }
            else {
                var putObject = angular.copy(managedRelation);
                putObject.label = popupData.relation.label;
                putObject.description = popupData.relation.description;
                if (!managedRelation.system) {
                    putObject.system = popupData.relation.system;
                }

                webvellaAdminService.updateRelation(putObject, successCallback, errorCallback)
            }
        };

        popupData.cancel = function () {
            $modalInstance.dismiss('cancel');
        };

        /// Aux
        function successCallback(response) {
            ngToast.create({
                className: 'success',
                content: '<span class="go-green">Success:</span> ' + 'The entity was successfully created'
            });
            $modalInstance.close('success');
            webvellaRootService.GoToState($state, $state.current.name, {});
        }

        function errorCallback(response) {
            var location = $location;
            //Process the response and generate the validation Messages
            webvellaRootService.generateValidationMessages(response, popupData, popupData.entity, location);
        }


        //Delete relation
        popupData.deleteRelationModal = function () {
            var modalInstance = $modal.open({
                animation: false,
                templateUrl: 'deleteRelationModal.html',
                controller: 'DeleteRelationModalController',
                controllerAs: "popupData",
                size: "",
                resolve: {
                    parentPopupData: function () { return popupData; }
                }
            });
        }

        $log.debug('webvellaAdmin>entities>CreateRelationModalController> END controller.exec ' + moment().format('HH:mm:ss SSSS'));
    };



    //// Modal Controllers
    DeleteRelationModalController.$inject = ['parentPopupData', '$modalInstance', '$log', 'webvellaAdminService', 'ngToast', '$timeout', '$state'];

    /* @ngInject */
    function DeleteRelationModalController(parentPopupData, $modalInstance, $log, webvellaAdminService, ngToast, $timeout, $state) {
    	$log.debug('webvellaAdmin>entities>deleteRelationModal> START controller.exec ' + moment().format('HH:mm:ss SSSS'));
        /* jshint validthis:true */
        var popupData = this;
        popupData.parentData = parentPopupData;

        popupData.ok = function () {
            webvellaAdminService.deleteRelation(popupData.parentData.relation.id, successCallback, errorCallback);
        };

        popupData.cancel = function () {
            $modalInstance.dismiss('cancel');
        };

        /// Aux
        function successCallback(response) {
            ngToast.create({
                className: 'success',
                content: '<span class="go-green">Success:</span> ' + response.message
            });
            $modalInstance.close('success');
            popupData.parentData.modalInstance.close('success');
            $timeout(function () {
                $state.go("webvella-admin-entity-relations", { name: popupData.parentData.currentEntity.name }, { reload: true });
            }, 0);
        }

        function errorCallback(response) {
            popupData.hasError = true;
            popupData.errorMessage = response.message;


        }
        $log.debug('webvellaAdmin>entities>deleteRelationModal> END controller.exec ' + moment().format('HH:mm:ss SSSS'));
    };




})();


