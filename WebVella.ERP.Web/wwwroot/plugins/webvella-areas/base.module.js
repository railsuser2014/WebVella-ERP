﻿/* base.module.js */

/**
* @desc this the base module of the Desktop plugin
*/

(function () {
    'use strict';

    angular
        .module('webvellaAreas', ['ui.router'])
        .config(config)
        .run(run)
        .controller('WebVellaAreasBaseController', controller);

    //#region << Configuration >>
    config.$inject = ['$stateProvider'];
    /* @ngInject */
    function config($stateProvider) {
        $stateProvider.state('webvella-areas-base', {
            abstract: true,
            url: '/areas/:areaName/:entityName', //will be added to all children states
            views: {
                "rootView": {
                    controller: 'WebVellaAreasBaseController',
                    templateUrl: '/plugins/webvella-areas/base.view.html',
                    controllerAs: 'pluginData'
                }
            },
            resolve: {
                //here you can resolve any plugin wide data you need. It will be available for all children states. Parent resolved objects can be injected in the functions too
                pageTitle: function () {
                    return "Webvella ERP";
                },
                resolvedSitemap: resolveSitemap,
                resolvedCurrentUser: resolveCurrentUser,
                resolvedCurrentEntityMeta: resolveCurrentEntityMeta,
                resolvedEntityRelationsList: resolveEntityRelationsList,
                checkedAccessPermission: checkAccessPermission
            }
        });
    };
    //#endregion

    //#region << Run >>
    run.$inject = ['$log', 'webvellaAreasService', 'webvellaDesktopBrowsenavFactory', '$rootScope'];
    /* @ngInject */
    function run($log, webvellaAreasService, webvellaDesktopBrowsenavFactory, $rootScope) {
    	$log.debug('webvellaAreas>base> BEGIN module.run ' + moment().format('HH:mm:ss SSSS'));

    	$log.debug('webvellaAreas>base> END module.run ' + moment().format('HH:mm:ss SSSS'));
    };
    //#endregion


	//#region << Resolve Function >>

    resolveSitemap.$inject = ['$q', '$log', 'webvellaRootService'];
	/* @ngInject */
    function resolveSitemap($q, $log, webvellaRootService) {
    	$log.debug('webvellaAreas>base>resolveSitemap> BEGIN sitemap resolved ' + moment().format('HH:mm:ss SSSS'));
    	// Initialize
    	var defer = $q.defer();

    	// Process
    	function successCallback(response) {
    		defer.resolve(response.object);
    	}

    	function errorCallback(response) {
    		defer.reject(response.message);
    	}
    	webvellaRootService.getSitemap(successCallback, errorCallback);
    	
    	// Return
    	$log.debug('webvellaAreas>base>resolveSitemap> END sitemap resolved ' + moment().format('HH:mm:ss SSSS'));
    	return defer.promise;
    }

    resolveCurrentUser.$inject = ['$q', '$log', 'webvellaAdminService', 'webvellaRootService', '$state', '$stateParams'];
	/* @ngInject */
    function resolveCurrentUser($q, $log, webvellaAdminService, webvellaRootService, $state, $stateParams) {
    	$log.debug('webvellaAreas>base>resolveCurrentUser> BEGIN user resolved ' + moment().format('HH:mm:ss SSSS'));
    	// Initialize
    	var defer = $q.defer();
    	// Process
    	var currentUser = webvellaRootService.getCurrentUser();
    	if (currentUser != null) {
    		defer.resolve(currentUser);
    	}
    	else {
    		defer.reject(null);
    	}

    	// Return
    	$log.debug('webvellaAreas>base>resolveCurrentUser> END user resolved ' + moment().format('HH:mm:ss SSSS'));
    	return defer.promise;
    }

    resolveCurrentEntityMeta.$inject = ['$q', '$log', 'webvellaAdminService', '$state', '$stateParams'];
	/* @ngInject */
    function resolveCurrentEntityMeta($q, $log, webvellaAdminService, $state, $stateParams) {
    	$log.debug('webvellaAdmin>entity-records> BEGIN entity list resolved ' + moment().format('HH:mm:ss SSSS'));
    	// Initialize
    	var defer = $q.defer();

    	// Process
    	function successCallback(response) {
    		defer.resolve(response.object);
    	}

    	function errorCallback(response) {
    		defer.reject(response.message);
    	}

    	webvellaAdminService.getEntityMeta($stateParams.entityName, successCallback, errorCallback);

    	// Return
    	$log.debug('webvellaDesktop>resolveCurrentEntityMeta> END state.resolved ' + moment().format('HH:mm:ss SSSS'));
    	return defer.promise;
    }

    resolveEntityRelationsList.$inject = ['$q', '$log', 'webvellaAdminService', '$stateParams', '$state', '$timeout'];
	/* @ngInject */
    function resolveEntityRelationsList($q, $log, webvellaAdminService, $stateParams, $state, $timeout) {
    	$log.debug('webvellaAdmin>entity-details> BEGIN state.resolved ' + moment().format('HH:mm:ss SSSS'));
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

    	webvellaAdminService.getRelationsList(successCallback, errorCallback);

    	// Return
    	$log.debug('webvellaAdmin>entity-details> END state.resolved ' + moment().format('HH:mm:ss SSSS'));
    	return defer.promise;
    }

    checkAccessPermission.$inject = ['$q', '$log', 'webvellaRootService', '$stateParams', 'resolvedSitemap', 'resolvedCurrentUser', 'ngToast'];
	/* @ngInject */
    function checkAccessPermission($q, $log, webvellaRootService, $stateParams, resolvedSitemap, resolvedCurrentUser, ngToast) {
    	$log.debug('webvellaAreas>entities> BEGIN check access permission ' + moment().format('HH:mm:ss SSSS'));
    	var defer = $q.defer();
    	var messageContent = '<span class="go-red">No access:</span> You do not have access to the <span class="go-red">' + $stateParams.areaName + '</span> area';
    	var accessPermission = webvellaRootService.applyAreaAccessPolicy($stateParams.areaName, resolvedCurrentUser, resolvedSitemap);
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

	//#endregion

    //#region << Controller >>
    controller.$inject = ['$log', '$stateParams', 'webvellaRootService', 'resolvedCurrentUser', 'resolvedSitemap', 'ngToast', '$window'];
    /* @ngInject */
    function controller($log, $stateParams, webvellaRootService, resolvedCurrentUser, resolvedSitemap, ngToast, $window) {
    	$log.debug('webvellaAreas>base> BEGIN controller.exec ' + moment().format('HH:mm:ss SSSS'));
        /* jshint validthis:true */
    	var pluginData = this;
        $log.debug('webvellaAreas>base> END controller.exec ' + moment().format('HH:mm:ss SSSS'));
    }
    //#endregion

})();
