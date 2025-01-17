﻿/* entity-view-manage.module.js */

/**
* @desc this module manages a single entity view in the admin screen
*/

(function () {
    'use strict';

    angular
        .module('webvellaAdmin') //only gets the module, already initialized in the base.module of the plugin. The lack of dependency [] makes the difference.
        .config(config)
        .controller('WebVellaAdminEntityViewManageController', controller)
        .controller('ManageSectionModalController', ManageSectionModalController)
        .controller('ManageRowModalController', ManageRowModalController)
        .controller('ManageItemModalController', ManageItemModalController);

    //#region << Configuration >> /////////////////////////
    config.$inject = ['$stateProvider'];
    /* @ngInject */
    function config($stateProvider) {
        $stateProvider.state('webvella-admin-entity-view-manage', {
            parent: 'webvella-admin-base',
            url: '/entities/:entityName/views/:viewName',
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
                    controller: 'WebVellaAdminEntityViewManageController',
                    templateUrl: '/plugins/webvella-admin/entity-view-manage.view.html',
                    controllerAs: 'contentData'
                }
            },
            resolve: {
            	checkedAccessPermission: checkAccessPermission,
                resolvedCurrentEntityMeta: resolveCurrentEntityMeta,
                resolvedViewLibrary: resolveViewLibrary
            },
            data: {

            }
        });
    };
    //#endregion

	//#region << Resolve >> ///////////////////////////////
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

    resolveViewLibrary.$inject = ['$q', '$log', 'webvellaAdminService', '$stateParams', '$state', '$timeout'];
    /* @ngInject */
    function resolveViewLibrary($q, $log, webvellaAdminService, $stateParams, $state, $timeout) {
    	$log.debug('webvellaAdmin>entity-views>resolveViewAvailableItems BEGIN state.resolved ' + moment().format('HH:mm:ss SSSS'));
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
            	//Remove the current view from the list to avoid loop
            	var libraryWithoutTheCurrentView = [];
            	for (var i = 0; i < response.object.length; i++) {
            		if (response.object[i].type != "view") {
            			libraryWithoutTheCurrentView.push(response.object[i]);
            		}
            		else if (response.object[i].type == "view" && response.object[i].viewName != $stateParams.viewName) {
            			libraryWithoutTheCurrentView.push(response.object[i]);
            		}
            	}
            	defer.resolve(libraryWithoutTheCurrentView);
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

        webvellaAdminService.getEntityViewLibrary($stateParams.entityName, successCallback, errorCallback);

        // Return
        $log.debug('webvellaAdmin>entity-views>resolveViewAvailableItems END state.resolved ' + moment().format('HH:mm:ss SSSS'));
        return defer.promise;
    }

    //#endregion

    //#region << Controller >> ////////////////////////////
    controller.$inject = ['$scope', '$log', '$rootScope', '$state','$stateParams', 'pageTitle', '$modal',
                            'resolvedCurrentEntityMeta', 'webvellaAdminService', 'ngToast', 'resolvedViewLibrary'];
    /* @ngInject */
    function controller($scope, $log, $rootScope, $state,$stateParams, pageTitle, $modal,
                        resolvedCurrentEntityMeta, webvellaAdminService, ngToast, resolvedViewLibrary) {
    	$log.debug('webvellaAdmin>entity-details> START controller.exec ' + moment().format('HH:mm:ss SSSS'));

        /* jshint validthis:true */
        var contentData = this;
        //#region << Initialize Current Entity >>
        contentData.entity = angular.copy(resolvedCurrentEntityMeta);
        //#endregion

        //#region << Update page title & Hide side menu>>
        contentData.pageTitle = "Entity Views | " + pageTitle;
        $rootScope.$emit("application-pageTitle-update", contentData.pageTitle);
        //Hide side menu
        $rootScope.$emit("application-body-sidebar-menu-isVisible-update", false);
        $log.debug('rootScope>events> "application-body-sidebar-menu-isVisible-update" emitted ' + moment().format('HH:mm:ss SSSS'));
        $scope.$on("$destroy", function () {
            $rootScope.$emit("application-body-sidebar-menu-isVisible-update", true);
            $log.debug('rootScope>events> "application-body-sidebar-menu-isVisible-update" emitted ' + moment().format('HH:mm:ss SSSS'));
        });
        //#endregion

    	//#region << Initialize View and Content Region >>
        contentData.view = {};
        for (var i = 0; i < contentData.entity.recordViews.length; i++) {
        	if (contentData.entity.recordViews[i].name == $stateParams.viewName) {
        		contentData.view = angular.copy(contentData.entity.recordViews[i]);
        	}
        }
        contentData.viewContentRegion = {};
        for (var i = 0; i < contentData.view.regions.length; i++) {
        	if (contentData.view.regions[i].name === "content") {
        		contentData.viewContentRegion = contentData.view.regions[i];
        	}
        }
    	//Get fields already used in the view so they need to be removed from the library
        var usedItemsArray = webvellaAdminService.getItemsFromRegion(contentData.viewContentRegion);

        contentData.tempLibrary = {};
        contentData.tempLibrary.items = angular.copy(resolvedViewLibrary);
        contentData.tempLibrary.items = contentData.tempLibrary.items.sort(function (a, b) {
        	if (a.type < b.type) return -1;
        	if (a.type > b.type) return 1;
        	return 0;
        });
        contentData.library = {};
        contentData.library.items = [];
        contentData.tempLibrary.items.forEach(function (item) {
        	var notUsed = true;
        	for (var k = 0; k < usedItemsArray.length; k++) {
        		if (item.type === "field" && usedItemsArray[k].type === "field"
						&& item.fieldId === usedItemsArray[k].fieldId) {
        			notUsed = false;
        		}
        		else if (item.type === "fieldFromRelation" && usedItemsArray[k].type === "fieldFromRelation"
						&& item.fieldId === usedItemsArray[k].fieldId && item.relationId === usedItemsArray[k].relationId) {
        			notUsed = false;
        		}
        		else if (item.type === "view" && usedItemsArray[k].type === "view" && item.viewId === usedItemsArray[k].viewId) {
        			notUsed = false;
        		}
        		else if (item.type === "viewFromRelation" && usedItemsArray[k].type === "viewFromRelation" && item.viewId === usedItemsArray[k].viewId) {
        			notUsed = false;
        		}
        		else if (item.type === "list" && usedItemsArray[k].type === "list" && item.listId === usedItemsArray[k].listId) {
        			notUsed = false;
        		}
        		else if (item.type === "listFromRelation" && usedItemsArray[k].type === "listFromRelation" && item.listId === usedItemsArray[k].listId) {
        			notUsed = false;
        		}
        	}
        	if (notUsed) {
        		//var search = "";
        		//if (item.type != null) {
        		//	search += item.type + " ";
        		//}
        		//if (item.tag != null) {
        		//	search += item.tag + " ";
        		//}
        		//if (item.fieldName != null) {
        		//	search += item.fieldName + " ";
        		//}
        		//if (item.fieldLabel != null) {
        		//	search += item.fieldLabel + " ";
        		//}
        		//if (item.entityName != null) {
        		//	search += item.entityName + " ";
        		//}
        		//if (item.entityLabel != null) {
        		//	search += item.entityLabel + " ";
        		//}
        		//if (item.viewName != null) {
        		//	search += item.viewName + " ";
        		//}
        		//if (item.viewLabel != null) {
        		//	search += item.viewLabel + " ";
        		//}
        		//if (item.listName != null) {
        		//	search += item.listName + " ";
        		//}
        		//if (item.listLabel != null) {
        		//	search += item.listLabel + " ";
        		//}
        		//if (item.entityLabelPlural != null) {
        		//	search += item.entityLabelPlural + " ";
        		//}
        		//item.search = search;
        		contentData.library.items.push(item);
        	}
        });



        //#endregion

        //#region << Section Management >>

        //Create or Update view section
        contentData.manageSectionModalOpen = function (sectionObj, weight) {
            var modalInstance = $modal.open({
                animation: false,
                templateUrl: 'manageSectionModal.html',
                controller: 'ManageSectionModalController',
                controllerAs: "popupData",
                size: "",
                resolve: {
                    parentData: function () { return contentData; },
                    section: function () { return sectionObj },
                    weight: function () { return weight },
                }
            });

        }

        //Remove section
        var tempCopyView = {};
        var tempCopyViewRegion = {}
        contentData.removeSection = function (id) {
            var isConfirmed = confirm("Are you sure that you need to remove this section?");
            if (isConfirmed == true) {
                // 1. Copy the view and contentRegion in a temp object
                tempCopyView = angular.copy(contentData.view);
                tempCopyViewRegion = angular.copy(contentData.viewContentRegion);
                // 2. Apply the change to the temp object
                tempCopyViewRegion.sections = webvellaAdminService.safeRemoveArrayPlace(tempCopyViewRegion.sections, id)
                // 3. Apply the changes of the temp ContentViewRegion to the temp view object
                for (var i = 0; i < tempCopyView.regions.length; i++) {
                    if (tempCopyView.regions[i].name === "content") {
                        tempCopyView.regions[i] = tempCopyViewRegion;
                    }
                }
                //Try update with the new view
                webvellaAdminService.updateEntityView(tempCopyView, contentData.entity.name, successSectionRemoveCallback, errorSectionRemoveCallback);

            }
        }

        function successSectionRemoveCallback(response) {
            ngToast.create({
                className: 'success',
                content: '<span class="go-green">Success:</span> ' + response.message
            });

            //Initialize both view and the content region with the new value
            contentData.view = tempCopyView;
            contentData.viewContentRegion = tempCopyViewRegion;
        }
        function errorSectionRemoveCallback(response) {
            ngToast.create({
                className: 'error',
                content: '<span class="go-red">Error:</span> ' + response.message
            });
        }

        //#endregion

        //#region << Row Management >>

        //Create view row
        contentData.manageRowModalOpen = function (rowObj, sectionObj, weight) {
            var modalInstance = $modal.open({
                animation: false,
                templateUrl: 'manageRowModal.html',
                controller: 'ManageRowModalController',
                controllerAs: "popupData",
                size: "",
                resolve: {
                    parentData: function () { return contentData; },
                    row: function () { return rowObj },
                    section: function () { return sectionObj },
                    weight: function () { return weight },
                }
            });

        }

        //Remove row
        contentData.removeRow = function (id, sectionId) {
            var isConfirmed = confirm("Are you sure that you need to remove this row?");
            if (isConfirmed == true) {
                // 1. Copy the view and contentRegion in a temp object
                var tempCopyView = angular.copy(contentData.view);
                var tempCopyViewRegion = angular.copy(contentData.viewContentRegion);
                // 2. Apply the change to the temp object
                for (var m = 0; m < tempCopyViewRegion.sections.length; m++) {
                    if (tempCopyViewRegion.sections[m].id == sectionId) {
                        tempCopyViewRegion.sections[m].rows = webvellaAdminService.safeRemoveArrayPlace(tempCopyViewRegion.sections[m].rows, id)
                    }
                }
                // 3. Apply the changes of the temp ContentViewRegion to the temp view object
                for (var i = 0; i < tempCopyView.regions.length; i++) {
                    if (tempCopyView.regions[i].name === "content") {
                        tempCopyView.regions[i] = tempCopyViewRegion;
                    }
                }
                //Try update with the new view
                webvellaAdminService.updateEntityView(tempCopyView, contentData.entity.name, successRowRemoveCallback, errorRowRemoveCallback);

            }
        }
        function successRowRemoveCallback(response) {
            ngToast.create({
                className: 'success',
                content: '<span class="go-green">Success:</span> ' + response.message
            });

        	//Initialize both view and the content region with the new value

            contentData.view = response.object;
            for (var i = 0; i < response.object.regions.length; i++) {
            	if (response.object.regions[i].name === "content") {
            		contentData.viewContentRegion = response.object.regions[i];
            	}
            }
        }
        function errorRowRemoveCallback(response) {
            ngToast.create({
                className: 'error',
                content: '<span class="go-red">Error:</span> ' + response.message
            });
        }

        //#endregion

        //#region << Drag & Drop Management >>

        function executeDragViewChange(eventObj) {
            //#region << 1.Define functions >>
            var moveSuccess, moveFailure, successCallback, errorCallback, openItemSettingsModal;

            openItemSettingsModal = function () {
                var modalInstance = $modal.open({
                    animation: false,
                    templateUrl: 'manageItemModal.html',
                    controller: 'ManageItemModalController',
                    controllerAs: "popupData",
                    size: "",
                    resolve: {
                        parentData: function () { return contentData; },
                        //row: function () { return rowObj },
                        //section: function () { return sectionObj },
                        //place: function () { return place },
                    }
                });
            }
            moveSuccess = function () {
                // Items should be able to be copied if it is not field, view or list
            	if (eventObj.source.itemScope.item.type !== "field"
				&& eventObj.source.itemScope.item.type !== "fieldFromRelation"
                    && eventObj.source.itemScope.item.type !== "view"
					&& eventObj.source.itemScope.item.type !== "viewFromRelation"
                    && eventObj.source.itemScope.item.type !== "list"
					&& eventObj.source.itemScope.item.type !== "listFromRelation") {
                    var objectCopy = angular.copy(eventObj.source.itemScope.item);
                    eventObj.source.itemScope.sortableScope.insertItem(eventObj.source.index, objectCopy);
                }

            };
            moveFailure = function () {
                eventObj.dest.sortableScope.removeItem(eventObj.dest.index);
                eventObj.source.itemScope.sortableScope.insertItem(eventObj.source.index, eventObj.source.itemScope.item);
            };

            function successCallback(response) {
                if (response.success) {
                    ngToast.create({
                        className: 'success',
                        content: '<span class="go-green">Success:</span> ' + response.message
                    });

                    moveSuccess();
                }
                else {
                	errorCallback(response);
                	moveFailure();
                }
            }

            function errorCallback(response) {
                ngToast.create({
                    className: 'error',
                    content: '<span class="go-red">Error:</span> ' + response.message
                });
                moveFailure();
            }
            //#endregion

            if (eventObj.source.itemScope.item.type != "field"
                && eventObj.source.itemScope.item.type != "view"
				&& eventObj.source.itemScope.item.type != "viewFromRelation"
				&& eventObj.source.itemScope.item.type !== "fieldFromRelation"
                && eventObj.source.itemScope.item.type != "list"
				&& eventObj.source.itemScope.item.type != "listFromRelation") {
                //can be managed
                openItemSettingsModal();
            }
            else {
                //cannot be managed
                //1. Update the view 
                for (var i = 0; i < contentData.view.regions.length; i++) {
                    if (contentData.view.regions[i].name === "content") {
                    	var cleanedRegion = angular.copy(contentData.viewContentRegion);
                    	for (var j = 0; j < cleanedRegion.sections.length; j++) {
                    		delete cleanedRegion.sections[j]["$$hashKey"];
                    		for (var g = 0; g < cleanedRegion.sections[j].rows.length; g++) {
                    			delete cleanedRegion.sections[j].rows[g]["$$hashKey"];
                    			for (var k = 0; k < cleanedRegion.sections[j].rows[g].columns.length; k++) {
                    				delete cleanedRegion.sections[j].rows[g].columns[k]["$$hashKey"];
                    				for (var m = 0; m < cleanedRegion.sections[j].rows[g].columns[k].items.length; m++) {
                    					var newFieldObject = {};
                    					newFieldObject.type = cleanedRegion.sections[j].rows[g].columns[k].items[m].type;
                    					switch (cleanedRegion.sections[j].rows[g].columns[k].items[m].type) {
                    						case "field":
                    							newFieldObject.fieldName = cleanedRegion.sections[j].rows[g].columns[k].items[m].meta.name;
                    							break;
                    						case "fieldFromRelation":
                    							newFieldObject.fieldName = cleanedRegion.sections[j].rows[g].columns[k].items[m].meta.name;
                    							newFieldObject.relationName = cleanedRegion.sections[j].rows[g].columns[k].items[m].relationName;
                    							break;
                    						case "view":
                    							newFieldObject.viewName = cleanedRegion.sections[j].rows[g].columns[k].items[m].meta.name;
                    							break;
                    						case "viewFromRelation":
                    							newFieldObject.viewName = cleanedRegion.sections[j].rows[g].columns[k].items[m].meta.name;
                    							newFieldObject.relationName = cleanedRegion.sections[j].rows[g].columns[k].items[m].relationName;
                    							break;
                    						case "list":
                    							newFieldObject.listName = cleanedRegion.sections[j].rows[g].columns[k].items[m].meta.name;
                    							break;
                    						case "listFromRelation":
                    							newFieldObject.listName = cleanedRegion.sections[j].rows[g].columns[k].items[m].meta.name;
                    							newFieldObject.relationName = cleanedRegion.sections[j].rows[g].columns[k].items[m].relationName;
                    							break;
                    					}
                    					
                    					cleanedRegion.sections[j].rows[g].columns[k].items[m] = newFieldObject;
                    				}
                    			}
                    		}
                    	}
                    	contentData.view.regions[i] = cleanedRegion;
                    }
                }
                //2. Call the service
                webvellaAdminService.updateEntityView(contentData.view, contentData.entity.name, successCallback, errorCallback);
            }

        }

        contentData.dragControlListeners = {
            accept: function (sourceItemHandleScope, destSortableScope) {
                //for (var i = 0; i < destSortableScope.modelValue.length; i++) {
                //    if (destSortableScope.modelValue[i].id == sourceItemHandleScope.item.id) {
                //        return false;
                //        break;
                //    }
                //}

                return true
            },
            itemMoved: function (eventObj) {
                //Item is moved from one column to another
                executeDragViewChange(eventObj);
            },
            orderChanged: function (eventObj) {
                //Item is moved within the same column
                executeDragViewChange(eventObj);
            }
        };

        //#endregion
        $log.debug('webvellaAdmin>entity-details> END controller.exec ' + moment().format('HH:mm:ss SSSS'));

    }
    //#endregion

    //#region << Modal Controllers >> /////////////////////

    //Section Modal
    ManageSectionModalController.$inject = ['parentData', 'section', 'weight', '$modalInstance', '$log', 'webvellaAdminService', 'ngToast', '$timeout', '$state','$scope'];
    /* @ngInject */
    function ManageSectionModalController(parentData, section, weight, $modalInstance, $log, webvellaAdminService, ngToast, $timeout, $state,$scope) {
    	$log.debug('webvellaAdmin>entities>createSectionModal> START controller.exec ' + moment().format('HH:mm:ss SSSS'));
        /* jshint validthis:true */

        //#region << Initialize >>
        var popupData = this;
        popupData.section = null;
        popupData.isUpdate = true;
        popupData.isValid = true;
        if (section == null) {
            popupData.isUpdate = false;
            popupData.section = angular.copy(webvellaAdminService.initViewSection());
            popupData.section.weight = weight;
        }
        else {
            popupData.section = angular.copy(section);
        }
        //#endregion

        popupData.ok = function () {
            popupData.view = angular.copy(parentData.view);
            //Find the content region, which is subject of this screen
            popupData.viewContentRegion = {};
            for (var i = 0; i < popupData.view.regions.length; i++) {
                if (popupData.view.regions[i].name === "content") {
                    popupData.viewContentRegion = popupData.view.regions[i];
                }
            }
        	// Validate unique username on add. It cannot be managed on update
            if (!popupData.isUpdate) {
            	popupData.isValid = true;
            	for (var i = 0; i < popupData.viewContentRegion.sections.length; i++) {
            		if (popupData.viewContentRegion.sections[i].name == popupData.section.name) {
            			popupData.isValid = false;
            		}
            	}
            	if (!popupData.isValid) {
            		$scope.manageSection.name.$dirty = true;
            		$scope.manageSection.name.$invalid = true;
            		$scope.manageSection.name.$pristine = false;
            		$scope.manageSection.name.$setValidity("unique", false);
            	}
            }
            //#region << Update the temporary view object for submission >>
            if (popupData.isUpdate && popupData.isValid) {
                popupData.viewContentRegion.sections = webvellaAdminService.safeUpdateArrayPlace(popupData.section, popupData.viewContentRegion.sections);
            }
            else if (popupData.isValid) {
                popupData.viewContentRegion.sections = webvellaAdminService.safeAddArrayPlace(popupData.section, popupData.viewContentRegion.sections);
            }
            //#endregion

            if (popupData.isValid) {
            	//Update the view with the correct values for the content region
            	for (var i = 0; i < popupData.view.regions.length; i++) {
            		if (popupData.view.regions[i].name === "content") {
            			popupData.view.regions[i] = popupData.viewContentRegion;
            		}
            	}

            	webvellaAdminService.updateEntityView(popupData.view, parentData.entity.name, successCallback, errorCallback);
            }
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
            //Initialize both view and the content region
            parentData.view = angular.copy(response.object);
            for (var i = 0; i < parentData.view.regions.length; i++) {
                if (parentData.view.regions[i].name === "content") {
                    parentData.viewContentRegion = parentData.view.regions[i];
                }
            }


        }

        function errorCallback(response) {
            popupData.hasError = true;
            popupData.errorMessage = response.message;

        }
        $log.debug('webvellaAdmin>entities>createSectionModal> END controller.exec ' + moment().format('HH:mm:ss SSSS'));
    };

    //Row Modal
    ManageRowModalController.$inject = ['parentData', 'row', 'section', 'weight', '$modalInstance', '$log', 'webvellaAdminService', 'ngToast', '$timeout', '$state'];
    /* @ngInject */
    function ManageRowModalController(parentData, row, section, weight, $modalInstance, $log, webvellaAdminService, ngToast, $timeout, $state) {
    	$log.debug('webvellaAdmin>entities>createRowModal> START controller.exec ' + moment().format('HH:mm:ss SSSS'));
        /* jshint validthis:true */
        var popupData = this;
        popupData.section = angular.copy(section);
        popupData.rowOptions = [
		{
			key: 1,
			value:"One column"
		},
		{
			key: 2,
			value: "Two columns"
		},
		{
			key: 3,
			value: "Three columns"
		},
		{
			key: 4,
			value: "Four columns"
		}

        ];
        popupData.isUpdate = true;
        if (row == null) {
            popupData.isUpdate = false;
            popupData.row = angular.copy(webvellaAdminService.initViewRow(1));
            popupData.row.weight = angular.copy(weight);
            popupData.selectedRowOption = popupData.rowOptions[0];
        }
        else {
            popupData.row = angular.copy(row);
            for (var i = 0; i < popupData.rowOptions.length; i++) {
            	if(popupData.row.columns.length == popupData.rowOptions[i].key){
					popupData.selectedRowOption = popupData.rowOptions[i];
				}
            }
        }

        popupData.ok = function () {
            //#region << 1. Get the current view and currentContentRegion >>
            popupData.view = angular.copy(parentData.view);
            //Find the content region, which is subject of this screen
            popupData.viewContentRegion = {};
            for (var i = 0; i < popupData.view.regions.length; i++) {
                if (popupData.view.regions[i].name === "content") {
                    popupData.viewContentRegion = popupData.view.regions[i];
                }
            }
            //#endregion
            //#region << 2. In the current section and recalculate the rows position in it based on the requested change >>
            if (popupData.isUpdate) {
                //A. Check if the row's column differ from the original number
                var originalRowColumns = 0;
                var newRowColumns = angular.copy(popupData.selectedRowOption.key);
                for (var i = 0; i < parentData.viewContentRegion.sections.length; i++) {
                    if (parentData.viewContentRegion.sections[i].name == popupData.section.name) {
                        for (var j = 0; j < parentData.viewContentRegion.sections[i].rows.length; j++) {
                        	if (parseInt(parentData.viewContentRegion.sections[i].rows[j].weight) == parseInt(row.weight)) {
                                originalRowColumns = parentData.viewContentRegion.sections[i].rows[j].columns.length;
                            }
                        }
                    }
                }
                //B. If columns differ add to the end or remove from the end
                if (originalRowColumns > newRowColumns) {
                    //Columns need to be removed
                    var columnsToRemove = originalRowColumns - newRowColumns;
                    popupData.row.columns.splice(columnsToRemove * -1);

                }
                else if (originalRowColumns < newRowColumns) {
                    //Columns need to be added
                    var columnsToAdd = newRowColumns - originalRowColumns;

                    for (var m = 0; m < columnsToAdd; m++) {
                        var column = webvellaAdminService.initViewRowColumn(newRowColumns);
                        popupData.row.columns.push(column);
                    }
                }
                //C. Fix the gridColCount for each column
                var newGridColCount = 12 / newRowColumns;
                for (var i = 0; i < popupData.row.columns.length; i++) {
                    popupData.row.columns[i].gridColCount = newGridColCount;
                }
                //D. Update
                popupData.section.rows = webvellaAdminService.safeUpdateArrayPlace(popupData.row, popupData.section.rows);
            }
            else {
            	popupData.row.columns = webvellaAdminService.initViewRow(popupData.selectedRowOption.key).columns;
                popupData.section.rows = webvellaAdminService.safeAddArrayPlace(popupData.row, popupData.section.rows);
            }
            //#endregion
            //#region << 3. Update the contentRegion & Feed in the updated ContentRegion in the view>>
            for (var i = 0; i < popupData.viewContentRegion.sections.length; i++) {
                if (popupData.viewContentRegion.sections[i].id == popupData.section.id) {
                    popupData.viewContentRegion.sections[i] = popupData.section;
                }
            }
            for (var i = 0; i < popupData.view.regions.length; i++) {
                if (popupData.view.regions[i].name === "content") {
                    popupData.view.regions[i] = popupData.viewContentRegion;
                }
            }

            //#endregion
            //#region << 4. Call the view update service >>
            webvellaAdminService.updateEntityView(popupData.view, parentData.entity.name, successCallback, errorCallback);
            //#endregion
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
            //Initialize both view and the content region
            parentData.view = angular.copy(response.object);
            for (var i = 0; i < parentData.view.regions.length; i++) {
                if (parentData.view.regions[i].name === "content") {
                    parentData.viewContentRegion = parentData.view.regions[i];
                }
            }
        }

        function errorCallback(response) {
            popupData.hasError = true;
            popupData.errorMessage = response.message;

        }
        $log.debug('webvellaAdmin>entities>createRowModal> END controller.exec ' + moment().format('HH:mm:ss SSSS'));
    };

    //TODO - finish the manageable Item Modal and process
    ManageItemModalController.$inject = ['parentData', '$modalInstance', '$log', 'webvellaAdminService', 'ngToast', '$timeout', '$state'];
    /* @ngInject */
    function ManageItemModalController(parentData, $modalInstance, $log, webvellaAdminService, ngToast, $timeout, $state) {
    	$log.debug('webvellaAdmin>entities>createRowModal> START controller.exec ' + moment().format('HH:mm:ss SSSS'));
        /* jshint validthis:true */
        var popupData = this;


        popupData.ok = function () {

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
        }

        function errorCallback(response) {
            popupData.hasError = true;
            popupData.errorMessage = response.message;

        }
        $log.debug('webvellaAdmin>entities>createRowModal> END controller.exec ' + moment().format('HH:mm:ss SSSS'));
    };

    //#endregion

})();
